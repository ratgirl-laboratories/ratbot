using Microsoft.EntityFrameworkCore;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed partial class AdventureLeaderboardManager(
    DiscordSocketClient discordClient,
    AdventureLeaderboardClient client,
    AdventureLeaderboardComponentBuilder componentBuilder,
    AdventureAccessController accessController,
    IDbContextFactory<BotDbContext> dbContextFactory,
    IOptions<AdventureLeaderboardOptions> options,
    ILogger logger
) : BackgroundService
{
    private const MessageFlags LeaderboardMessageFlags = MessageFlags.ComponentsV2 | MessageFlags.SuppressNotification;

    private static readonly AllowedMentions UserMentionsOnly = new AllowedMentions(AllowedMentionTypes.Users);
    private readonly HashSet<ulong> _excludedUserIds = new HashSet<ulong>();

    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger = logger.ForContext<AdventureLeaderboardManager>();
    private readonly AdventureLeaderboardOptions _options = options.Value;
    private TrackedLeaderboardMessageSequence? _trackedMessageSequence;

    private static string BuildRenderHash(
        AdventureEntrySnapshot snapshot,
        IReadOnlySet<ulong> guildMemberUserIds,
        IReadOnlySet<ulong> excludedUserIds
    ) => $"{snapshot.Hash}:{string.Join(',', guildMemberUserIds.Order())}:{string.Join(',', excludedUserIds.Order())}";

    private static async Task<IReadOnlySet<ulong>> FindGuildMemberUserIdsAsync(
        IGuild guild,
        AdventureEntrySnapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        HashSet<ulong> memberIds = new HashSet<ulong>();
        RequestOptions requestOptions = new RequestOptions { CancelToken = cancellationToken };

        foreach (AdventureEntryRow row in snapshot.Rows)
        {
            if (!ulong.TryParse(row.UserId, out ulong userId))
                continue;

            IGuildUser? member = await guild.GetUserAsync(userId, CacheMode.AllowDownload, requestOptions).ConfigureAwait(false);

            if (member is not null)
                memberIds.Add(userId);
        }

        return memberIds;
    }

    private static Task ModifyLeaderboardMessageAsync(IUserMessage message, MessageComponent components, CancellationToken cancellationToken) =>
        message.ModifyAsync(
            properties =>
            {
                properties.Components = components;
                properties.AllowedMentions = UserMentionsOnly;
                properties.Flags = LeaderboardMessageFlags;
            },
            new RequestOptions { CancelToken = cancellationToken }
        );

    public async Task<IUserMessage> CreateLeaderboardMessageAsync(ITextChannel channel, int year, CancellationToken cancellationToken)
    {
        AdventureEntrySnapshot snapshot = await FetchSnapshotAsync(year, cancellationToken).ConfigureAwait(false);
        await SyncAdventureForumAccessAsync(channel.Guild.Id, snapshot, cancellationToken).ConfigureAwait(false);

        TrackedLeaderboardMessageSequence? previousSequence;
        IUserMessage message;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AdventureEntrySnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);

            ImmutableHashSet<ulong> guildMemberUserIds = (
                await FindGuildMemberUserIdsAsync(channel.Guild, visibleSnapshot, cancellationToken).ConfigureAwait(false)
            ).ToImmutableHashSet();

            string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);
            IReadOnlyList<MessageComponent> components = BuildComponents(visibleSnapshot, year, guildMemberUserIds);

            IReadOnlyList<IUserMessage> messages = await SendLeaderboardMessagesAsync(channel, components, cancellationToken).ConfigureAwait(false);

            message = messages[0];

            TrackedLeaderboardMessageSequence trackedSequence = new TrackedLeaderboardMessageSequence(
                channel.Guild.Id,
                channel.Id,
                messages.Select(x => x.Id).ToArray(),
                year,
                renderHash
            );

            await SaveTrackedMessageSequenceAsync(trackedSequence, cancellationToken).ConfigureAwait(false);

            previousSequence = _trackedMessageSequence;
            _trackedMessageSequence = trackedSequence;
        }
        finally
        {
            _lock.Release();
        }

        if (previousSequence.HasValue)
            await DeletePreviousMessagesAsync(previousSequence.Value, cancellationToken).ConfigureAwait(false);

        return message;
    }

    public async Task<bool> ExcludeUserAsync(ulong userId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            bool added = _excludedUserIds.Add(userId);

            try
            {
                if (_trackedMessageSequence.HasValue)
                    await UpdateTrackedMessageCoreAsync(_trackedMessageSequence.Value, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to update adventure leaderboard message after excluding user {UserId}.", userId);
            }

            return added;
        }
        finally
        {
            _lock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Adventure leaderboard update service started.");

        try
        {
            await LoadTrackedMessageAsync(stoppingToken).ConfigureAwait(false);

            using PeriodicTimer timer = new PeriodicTimer(_options.RefreshInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await UpdateTrackedMessageAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Adventure leaderboard update service is stopping.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Adventure leaderboard update service encountered an error.");
        }
    }

    private async Task AppendNewLeaderboardMessagesAsync(
        ITextChannel channel,
        int existingMessageCount,
        IReadOnlyList<MessageComponent> components,
        List<ulong> messageIds,
        CancellationToken cancellationToken
    )
    {
        if (components.Count <= existingMessageCount)
            return;

        IReadOnlyList<IUserMessage> newMessages = await SendLeaderboardMessagesAsync(
                channel,
                components.Skip(existingMessageCount).ToArray(),
                cancellationToken
            )
            .ConfigureAwait(false);

        messageIds.AddRange(newMessages.Select(x => x.Id));
    }

    private IReadOnlyList<MessageComponent> BuildComponents(AdventureEntrySnapshot snapshot, int year, ImmutableHashSet<ulong> guildMemberUserIds)
    {
        AdventureLeaderboardViewModel model = AdventureLeaderboardFormatter.Format(snapshot, year, guildMemberUserIds, DateTimeOffset.UtcNow);

        return AdventureLeaderboardComponentBuilder.Build(model);
    }

    private async Task DeleteMessageAsync(IMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await message.DeleteAsync(new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to delete adventure leaderboard message {MessageId}.", message.Id);
        }
    }

    private async Task DeletePreviousMessagesAsync(TrackedLeaderboardMessageSequence previousSequence, CancellationToken cancellationToken)
    {
        try
        {
            SocketGuild? guild = discordClient.GetGuild(previousSequence.GuildId);
            IMessageChannel? channel = guild?.GetTextChannel(previousSequence.ChannelId);

            if (channel is null)
            {
                _logger.Warning(
                    "Cannot delete previous adventure leaderboard message because channel {ChannelId} is unavailable.",
                    previousSequence.ChannelId
                );

                return;
            }

            foreach (ulong messageId in previousSequence.MessageIds)
            {
                IMessage? message = await channel
                    .GetMessageAsync(messageId, options: new RequestOptions { CancelToken = cancellationToken })
                    .ConfigureAwait(false);

                if (message is null)
                    continue;

                await DeleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to delete one or more previous adventure leaderboard messages.");
        }
    }

    private async Task DeleteSurplusLeaderboardMessagesAsync(
        IReadOnlyList<IUserMessage> messages,
        int requiredMessageCount,
        CancellationToken cancellationToken
    )
    {
        if (messages.Count <= requiredMessageCount)
            return;

        foreach (IUserMessage message in messages.Skip(requiredMessageCount))
            await DeleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdventureEntrySnapshot> FetchSnapshotAsync(int year, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdventureEntryDto> rows = await client.GetLeaderboardAsync(year, cancellationToken).ConfigureAwait(false);

        return AdventureEntrySnapshot.FromDtos(rows);
    }

    private async Task<(TrackedLeaderboardMessageTarget? Target, bool ShouldRecreateSequence)> FindTrackedMessageTargetAsync(
        TrackedLeaderboardMessageSequence trackedSequence,
        CancellationToken cancellationToken
    )
    {
        SocketGuild? guild = discordClient.GetGuild(trackedSequence.GuildId);
        ITextChannel? channel = guild?.GetTextChannel(trackedSequence.ChannelId);

        if (guild is null || channel is null)
        {
            _logger.Warning(
                "Cannot update adventure leaderboard because guild {GuildId} or channel {ChannelId} is unavailable.",
                trackedSequence.GuildId,
                trackedSequence.ChannelId
            );

            return (null, false);
        }

        List<IUserMessage> messages = new List<IUserMessage>();

        foreach (ulong messageId in trackedSequence.MessageIds)
        {
            IUserMessage? message =
                await channel.GetMessageAsync(messageId, options: new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false)
                as IUserMessage;

            if (message is null)
            {
                _logger.Warning("Adventure leaderboard message {MessageId} is unavailable.", messageId);
                return (new TrackedLeaderboardMessageTarget(guild, channel, messages), true);
            }

            messages.Add(message);
        }

        return (new TrackedLeaderboardMessageTarget(guild, channel, messages), false);
    }

    private async Task LoadTrackedMessageAsync(CancellationToken cancellationToken)
    {
        await using BotDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<AdventureLeaderboardMessageState> states = await dbContext
            .AdventureLeaderboardMessageState.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (states.Count == 0)
            return;

        AdventureLeaderboardMessageState firstState = states[0];

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _trackedMessageSequence = new TrackedLeaderboardMessageSequence(
                firstState.GuildId,
                firstState.ChannelId,
                states.Select(x => x.MessageId).ToArray(),
                firstState.Year,
                firstState.LastRenderHash
            );
        }
        finally
        {
            _lock.Release();
        }

        _logger.Information(
            "Loaded {MessageCount} persisted adventure leaderboard messages in channel {ChannelId} for year {Year}.",
            states.Count,
            firstState.ChannelId,
            firstState.Year
        );
    }

    private async Task<TrackedLeaderboardMessageSequence> ReconcileTrackedMessageSequenceAsync(
        TrackedLeaderboardMessageTarget target,
        TrackedLeaderboardMessageSequence trackedSequence,
        IReadOnlyList<MessageComponent> components,
        string renderHash,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<ulong> messageIds = await UpdateExistingLeaderboardMessagesAsync(target.Messages, components, cancellationToken)
            .ConfigureAwait(false);

        List<ulong> updatedMessageIds = new List<ulong>(messageIds);

        await AppendNewLeaderboardMessagesAsync(target.Channel, target.Messages.Count, components, updatedMessageIds, cancellationToken)
            .ConfigureAwait(false);

        await DeleteSurplusLeaderboardMessagesAsync(target.Messages, components.Count, cancellationToken).ConfigureAwait(false);

        TrackedLeaderboardMessageSequence updatedTrackedSequence = trackedSequence with
        {
            MessageIds = updatedMessageIds.ToArray(),
            LastRenderHash = renderHash,
        };

        await SaveTrackedMessageSequenceAsync(updatedTrackedSequence, cancellationToken).ConfigureAwait(false);

        return updatedTrackedSequence;
    }

    private async Task<TrackedLeaderboardMessageSequence> RecreateTrackedMessageSequenceAsync(
        ITextChannel channel,
        TrackedLeaderboardMessageSequence previousSequence,
        IReadOnlyList<MessageComponent> components,
        string renderHash,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<IUserMessage> messages = await SendLeaderboardMessagesAsync(channel, components, cancellationToken).ConfigureAwait(false);

        TrackedLeaderboardMessageSequence recreatedSequence = previousSequence with
        {
            MessageIds = messages.Select(x => x.Id).ToArray(),
            LastRenderHash = renderHash,
        };

        await SaveTrackedMessageSequenceAsync(recreatedSequence, cancellationToken).ConfigureAwait(false);
        await DeletePreviousMessagesAsync(previousSequence, cancellationToken).ConfigureAwait(false);

        return recreatedSequence;
    }

    private AdventureEntrySnapshot RemoveExcludedUsers(AdventureEntrySnapshot snapshot)
    {
        if (_excludedUserIds.Count == 0)
            return snapshot;

        return snapshot with
        {
            Rows = snapshot.Rows.Where(row => !ulong.TryParse(row.UserId, out ulong userId) || !_excludedUserIds.Contains(userId)).ToImmutableArray(),
        };
    }

    private async Task SaveTrackedMessageSequenceAsync(TrackedLeaderboardMessageSequence trackedSequence, CancellationToken cancellationToken)
    {
        await using BotDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.AdventureLeaderboardMessageState.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        for (int index = 0; index < trackedSequence.MessageIds.Count; index++)
            dbContext.AdventureLeaderboardMessageState.Add(
                AdventureLeaderboardMessageState.Create(
                    index + 1,
                    trackedSequence.GuildId,
                    trackedSequence.ChannelId,
                    trackedSequence.MessageIds[index],
                    trackedSequence.Year,
                    trackedSequence.LastRenderHash
                )
            );

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<IUserMessage>> SendLeaderboardMessagesAsync(
        ITextChannel channel,
        IReadOnlyList<MessageComponent> components,
        CancellationToken cancellationToken
    )
    {
        List<IUserMessage> messages = new List<IUserMessage>();

        foreach (MessageComponent component in components)
        {
            IUserMessage message = await channel
                .SendMessageAsync(
                    allowedMentions: UserMentionsOnly,
                    components: component,
                    flags: LeaderboardMessageFlags,
                    options: new RequestOptions { CancelToken = cancellationToken }
                )
                .ConfigureAwait(false);

            messages.Add(message);
        }

        return messages;
    }

    private async Task SyncAdventureForumAccessAsync(ulong guildId, AdventureEntrySnapshot snapshot, CancellationToken cancellationToken)
    {
        SocketGuild? guild = discordClient.GetGuild(guildId);

        if (guild is null)
        {
            _logger.Warning("Cannot sync adventure forum access because guild {GuildId} is unavailable.", guildId);
            return;
        }

        try
        {
            await accessController.UpdateAccessGrantsAsync(guild, snapshot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Adventure forum access sync failed.");
        }
    }

    private async Task<IReadOnlyList<ulong>> UpdateExistingLeaderboardMessagesAsync(
        IReadOnlyList<IUserMessage> messages,
        IReadOnlyList<MessageComponent> components,
        CancellationToken cancellationToken
    )
    {
        List<ulong> messageIds = new List<ulong>();
        int existingMessageCount = Math.Min(messages.Count, components.Count);

        for (int index = 0; index < existingMessageCount; index++)
        {
            await ModifyLeaderboardMessageAsync(messages[index], components[index], cancellationToken).ConfigureAwait(false);

            messageIds.Add(messages[index].Id);
        }

        return messageIds;
    }

    private async Task UpdateTrackedMessageAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_trackedMessageSequence.HasValue)
                return;

            await UpdateTrackedMessageCoreAsync(_trackedMessageSequence.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to update adventure leaderboard message.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task UpdateTrackedMessageCoreAsync(TrackedLeaderboardMessageSequence trackedSequence, CancellationToken cancellationToken)
    {
        AdventureEntrySnapshot snapshot = await FetchSnapshotAsync(trackedSequence.Year, cancellationToken).ConfigureAwait(false);

        await SyncAdventureForumAccessAsync(trackedSequence.GuildId, snapshot, cancellationToken).ConfigureAwait(false);

        AdventureEntrySnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);

        (TrackedLeaderboardMessageTarget? target, bool shouldRecreateSequence) = await FindTrackedMessageTargetAsync(
                trackedSequence,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!target.HasValue)
            return;

        ImmutableHashSet<ulong> guildMemberUserIds = (
            await FindGuildMemberUserIdsAsync(target.Value.Guild, visibleSnapshot, cancellationToken).ConfigureAwait(false)
        ).ToImmutableHashSet();

        string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);

        IReadOnlyList<MessageComponent> components = BuildComponents(visibleSnapshot, trackedSequence.Year, guildMemberUserIds);

        if (shouldRecreateSequence)
        {
            TrackedLeaderboardMessageSequence recreatedSequence = await RecreateTrackedMessageSequenceAsync(
                    target.Value.Channel,
                    trackedSequence,
                    components,
                    renderHash,
                    cancellationToken
                )
                .ConfigureAwait(false);

            _trackedMessageSequence = recreatedSequence;
            return;
        }

        if (
            string.Equals(renderHash, trackedSequence.LastRenderHash, StringComparison.Ordinal)
            && components.Count == trackedSequence.MessageIds.Count
        )
            return;

        TrackedLeaderboardMessageSequence updatedTrackedSequence = await ReconcileTrackedMessageSequenceAsync(
                target.Value,
                trackedSequence,
                components,
                renderHash,
                cancellationToken
            )
            .ConfigureAwait(false);

        _trackedMessageSequence = updatedTrackedSequence;
    }
}
