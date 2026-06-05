using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed partial class AdventureLeaderboardManager(
    DiscordSocketClient discordClient,
    AdventureLeaderboardClient client,
    AdventureLeaderboardComponentBuilder componentBuilder,
    AdventureAccessController accessController,
    IDbContextFactory<BotDbContext> dbContextFactory,
    IOptions<AdventureLeaderboardOptions> options,
    ILogger logger)
    : BackgroundService
{

    private const MessageFlags LeaderboardMessageFlags =
        MessageFlags.ComponentsV2 | MessageFlags.SuppressNotification;
    private static readonly AllowedMentions UserMentionsOnly = new AllowedMentions(AllowedMentionTypes.Users);

    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger = logger.ForContext<AdventureLeaderboardManager>();
    private readonly AdventureLeaderboardOptions _options = options.Value;
    private readonly HashSet<ulong> _excludedUserIds = new HashSet<ulong>();
    private TrackedLeaderboardMessage? _trackedMessage;

    private async static Task<IReadOnlySet<ulong>> FindGuildMemberUserIdsAsync(
        IGuild guild,
        AdventureEntrySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        HashSet<ulong> memberIds = new HashSet<ulong>();
        RequestOptions requestOptions = new RequestOptions { CancelToken = cancellationToken };

        foreach (AdventureEntryRow row in snapshot.Rows)
        {
            if (!ulong.TryParse(row.UserId, out ulong userId))
                continue;

            IGuildUser? member = await guild.GetUserAsync(userId, CacheMode.AllowDownload, requestOptions)
                .ConfigureAwait(false);

            if (member is not null)
                memberIds.Add(userId);
        }

        return memberIds;
    }

    private static string BuildRenderHash(
        AdventureEntrySnapshot snapshot,
        IReadOnlySet<ulong> guildMemberUserIds,
        IReadOnlySet<ulong> excludedUserIds) =>
        $"{snapshot.Hash}:{string.Join(',', guildMemberUserIds.Order())}:{string.Join(',', excludedUserIds.Order())}";

    public async Task<IUserMessage> CreateLeaderboardMessageAsync(
        ITextChannel channel,
        int year,
        CancellationToken cancellationToken)
    {
        AdventureEntrySnapshot snapshot = await FetchSnapshotAsync(year, cancellationToken).ConfigureAwait(false);
        await SyncAdventureForumAccessAsync(channel.Guild.Id, snapshot, cancellationToken).ConfigureAwait(false);

        TrackedLeaderboardMessage? previousMessage;
        IUserMessage message;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AdventureEntrySnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);

            ImmutableHashSet<ulong> guildMemberUserIds =
                (await FindGuildMemberUserIdsAsync(channel.Guild, visibleSnapshot, cancellationToken)
                    .ConfigureAwait(false))
                .ToImmutableHashSet();

            string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);
            MessageComponent components = BuildComponents(visibleSnapshot, year, guildMemberUserIds);

            message = await channel.SendMessageAsync(
                    allowedMentions: UserMentionsOnly,
                    components: components,
                    flags: LeaderboardMessageFlags)
                .ConfigureAwait(false);

            TrackedLeaderboardMessage trackedMessage =
                new TrackedLeaderboardMessage(channel.Guild.Id, channel.Id, message.Id, year, renderHash);

            await SaveTrackedMessageAsync(trackedMessage, cancellationToken).ConfigureAwait(false);

            previousMessage = _trackedMessage;
            _trackedMessage = trackedMessage;
        }
        finally
        {
            _lock.Release();
        }

        if (previousMessage.HasValue)
            await DeletePreviousMessageAsync(previousMessage.Value, cancellationToken).ConfigureAwait(false);

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
                if (_trackedMessage.HasValue)
                    await UpdateTrackedMessageCoreAsync(_trackedMessage.Value, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    ex,
                    "Failed to update adventure leaderboard message after excluding user {UserId}.",
                    userId);
            }

            return added;
        }
        finally
        {
            _lock.Release();
        }
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
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

    private async Task UpdateTrackedMessageAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_trackedMessage.HasValue)
                return;

            await UpdateTrackedMessageCoreAsync(_trackedMessage.Value, cancellationToken).ConfigureAwait(false);
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

    private async Task UpdateTrackedMessageCoreAsync(
        TrackedLeaderboardMessage trackedMessage,
        CancellationToken cancellationToken)
    {
        AdventureEntrySnapshot snapshot = await FetchSnapshotAsync(
                trackedMessage.Year,
                cancellationToken)
            .ConfigureAwait(false);

        await SyncAdventureForumAccessAsync(trackedMessage.GuildId, snapshot, cancellationToken).ConfigureAwait(false);

        AdventureEntrySnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);

        (TrackedLeaderboardMessageTarget? target, bool shouldClearPersistedState) =
            await FindTrackedMessageTargetAsync(trackedMessage, cancellationToken).ConfigureAwait(false);

        if (!target.HasValue)
        {
            if (shouldClearPersistedState)
            {
                await ClearTrackedMessageAsync(cancellationToken).ConfigureAwait(false);
                _trackedMessage = null;
            }

            return;
        }

        ImmutableHashSet<ulong> guildMemberUserIds =
            (await FindGuildMemberUserIdsAsync(target.Value.Guild, visibleSnapshot, cancellationToken)
                .ConfigureAwait(false))
            .ToImmutableHashSet();

        string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);

        if (string.Equals(renderHash, trackedMessage.LastRenderHash, StringComparison.Ordinal))
            return;

        MessageComponent components = BuildComponents(visibleSnapshot, trackedMessage.Year, guildMemberUserIds);

        await target.Value.Message.ModifyAsync(
                properties =>
                {
                    properties.Components = components;
                    properties.AllowedMentions = UserMentionsOnly;
                    properties.Flags = LeaderboardMessageFlags;
                },
                new RequestOptions { CancelToken = cancellationToken })
            .ConfigureAwait(false);

        TrackedLeaderboardMessage updatedTrackedMessage = trackedMessage with { LastRenderHash = renderHash };

        await SaveTrackedMessageAsync(updatedTrackedMessage, cancellationToken).ConfigureAwait(false);

        _trackedMessage = updatedTrackedMessage;
    }

    private async Task LoadTrackedMessageAsync(CancellationToken cancellationToken)
    {
        await using BotDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AdventureLeaderboardMessageState? state = await dbContext.AdventureLeaderboardMessageState
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == AdventureLeaderboardMessageState.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
            return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _trackedMessage = new TrackedLeaderboardMessage(
                state.GuildId,
                state.ChannelId,
                state.MessageId,
                state.Year,
                state.LastRenderHash);
        }
        finally
        {
            _lock.Release();
        }

        _logger.Information(
            "Loaded persisted adventure leaderboard message {MessageId} in channel {ChannelId} for year {Year}.",
            state.MessageId,
            state.ChannelId,
            state.Year);
    }

    private async Task SaveTrackedMessageAsync(
        TrackedLeaderboardMessage trackedMessage,
        CancellationToken cancellationToken)
    {
        await using BotDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.AdventureLeaderboardMessageState
            .Where(x => x.Id == AdventureLeaderboardMessageState.SingletonId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.AdventureLeaderboardMessageState.Add(
            AdventureLeaderboardMessageState.Create(
                trackedMessage.GuildId,
                trackedMessage.ChannelId,
                trackedMessage.MessageId,
                trackedMessage.Year,
                trackedMessage.LastRenderHash));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ClearTrackedMessageAsync(CancellationToken cancellationToken)
    {
        await using BotDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.AdventureLeaderboardMessageState
            .Where(x => x.Id == AdventureLeaderboardMessageState.SingletonId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AdventureEntrySnapshot> FetchSnapshotAsync(int year, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdventureEntryDto> rows =
            await client.GetLeaderboardAsync(year, cancellationToken).ConfigureAwait(false);

        return AdventureEntrySnapshot.FromDtos(rows);
    }

    private async Task SyncAdventureForumAccessAsync(
        ulong guildId,
        AdventureEntrySnapshot snapshot,
        CancellationToken cancellationToken)
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

    private AdventureEntrySnapshot RemoveExcludedUsers(AdventureEntrySnapshot snapshot)
    {
        if (_excludedUserIds.Count == 0)
            return snapshot;

        return snapshot with
        {
            Rows = snapshot.Rows
                .Where(row => !ulong.TryParse(row.UserId, out ulong userId) || !_excludedUserIds.Contains(userId))
                .ToImmutableArray(),
        };
    }

    private MessageComponent BuildComponents(
        AdventureEntrySnapshot snapshot,
        int year,
        ImmutableHashSet<ulong> guildMemberUserIds)
    {
        AdventureLeaderboardViewModel model = AdventureLeaderboardFormatter.Format(
            snapshot,
            year,
            guildMemberUserIds,
            DateTimeOffset.UtcNow);

        return componentBuilder.Build(model);
    }

    private async Task<(TrackedLeaderboardMessageTarget? Target, bool ShouldClearPersistedState)> FindTrackedMessageTargetAsync(
        TrackedLeaderboardMessage trackedMessage,
        CancellationToken cancellationToken)
    {
        SocketGuild? guild = discordClient.GetGuild(trackedMessage.GuildId);
        IMessageChannel? channel = guild?.GetTextChannel(trackedMessage.ChannelId);

        if (guild is null || channel is null)
        {
            _logger.Warning(
                "Cannot update adventure leaderboard because guild {GuildId} or channel {ChannelId} is unavailable.",
                trackedMessage.GuildId,
                trackedMessage.ChannelId);

            return (null, false);
        }

        IUserMessage? message = await channel.GetMessageAsync(
                trackedMessage.MessageId,
                options: new RequestOptions { CancelToken = cancellationToken })
            .ConfigureAwait(false) as IUserMessage;

        if (message is null)
        {
            _logger.Warning("Adventure leaderboard message {MessageId} is unavailable.", trackedMessage.MessageId);
            return (null, true);
        }

        return (new TrackedLeaderboardMessageTarget(guild, message), false);
    }

    private async Task DeletePreviousMessageAsync(
        TrackedLeaderboardMessage previousMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            SocketGuild? guild = discordClient.GetGuild(previousMessage.GuildId);
            IMessageChannel? channel = guild?.GetTextChannel(previousMessage.ChannelId);

            if (channel is null)
            {
                _logger.Warning(
                    "Cannot delete previous adventure leaderboard message because channel {ChannelId} is unavailable.",
                    previousMessage.ChannelId);

                return;
            }

            IMessage? message = await channel.GetMessageAsync(
                    previousMessage.MessageId,
                    options: new RequestOptions { CancelToken = cancellationToken })
                .ConfigureAwait(false);

            if (message is null)
                return;

            await message.DeleteAsync(new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Failed to delete previous adventure leaderboard message {MessageId}.",
                previousMessage.MessageId);
        }
    }
}
