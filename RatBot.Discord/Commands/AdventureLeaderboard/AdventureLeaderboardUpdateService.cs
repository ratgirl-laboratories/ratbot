using Microsoft.Extensions.Options;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardUpdateService(
    DiscordSocketClient discordClient,
    AdventureLeaderboardClient client,
    AdventureLeaderboardComponentBuilder componentBuilder,
    IOptions<AdventureLeaderboardOptions> options,
    ILogger logger)
    : BackgroundService
{
    private static readonly AllowedMentions UserMentionsOnly = new AllowedMentions(AllowedMentionTypes.Users);
    private const MessageFlags LeaderboardMessageFlags =
        MessageFlags.ComponentsV2 | MessageFlags.SuppressNotification;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger = logger.ForContext<AdventureLeaderboardUpdateService>();
    private readonly AdventureLeaderboardOptions _options = options.Value;
    private readonly HashSet<ulong> _excludedUserIds = new HashSet<ulong>();
    private TrackedLeaderboardMessage? _trackedMessage;

    public async Task<IUserMessage> CreateLeaderboardMessageAsync(
        ITextChannel channel,
        int year,
        CancellationToken cancellationToken)
    {
        AdventureLeaderboardSnapshot snapshot = await FetchSnapshotAsync(year, cancellationToken).ConfigureAwait(false);
        TrackedLeaderboardMessage? previousMessage;
        IUserMessage message;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AdventureLeaderboardSnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);
            IReadOnlySet<ulong> guildMemberUserIds =
                await FindGuildMemberUserIdsAsync(channel.Guild, visibleSnapshot, cancellationToken).ConfigureAwait(false);
            string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);
            MessageComponent components = BuildComponents(visibleSnapshot, year, guildMemberUserIds);

            message = await channel.SendMessageAsync(
                allowedMentions: UserMentionsOnly,
                components: components,
                flags: LeaderboardMessageFlags).ConfigureAwait(false);

            previousMessage = _trackedMessage;
            _trackedMessage = new TrackedLeaderboardMessage(channel.Guild.Id, channel.Id, message.Id, year, renderHash);
        }
        finally
        {
            _lock.Release();
        }

        if (previousMessage is not null)
            await DeletePreviousMessageAsync(previousMessage, cancellationToken).ConfigureAwait(false);

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
                if (_trackedMessage is not null)
                    await UpdateTrackedMessageCoreAsync(_trackedMessage, cancellationToken).ConfigureAwait(false);
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

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Adventure leaderboard update service started.");

        try
        {
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
            if (_trackedMessage is null)
                return;

            await UpdateTrackedMessageCoreAsync(_trackedMessage, cancellationToken).ConfigureAwait(false);
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
        AdventureLeaderboardSnapshot snapshot = await FetchSnapshotAsync(
            trackedMessage.Year,
            cancellationToken).ConfigureAwait(false);

        AdventureLeaderboardSnapshot visibleSnapshot = RemoveExcludedUsers(snapshot);

        TrackedLeaderboardMessageTarget? target =
            await FindTrackedMessageTargetAsync(trackedMessage, cancellationToken).ConfigureAwait(false);

        if (target is null)
            return;

        IReadOnlySet<ulong> guildMemberUserIds =
            await FindGuildMemberUserIdsAsync(target.Guild, visibleSnapshot, cancellationToken).ConfigureAwait(false);
        string renderHash = BuildRenderHash(snapshot, guildMemberUserIds, _excludedUserIds);

        if (string.Equals(renderHash, trackedMessage.LastRenderHash, StringComparison.Ordinal))
            return;

        MessageComponent components = BuildComponents(visibleSnapshot, trackedMessage.Year, guildMemberUserIds);

        await target.Message.ModifyAsync(
            properties =>
            {
                properties.Components = components;
                properties.AllowedMentions = UserMentionsOnly;
                properties.Flags = LeaderboardMessageFlags;
            },
            new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false);

        _trackedMessage = trackedMessage with { LastRenderHash = renderHash };
    }

    private async Task<AdventureLeaderboardSnapshot> FetchSnapshotAsync(int year, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdventureLeaderboardEntryDto> rows =
            await client.GetLeaderboardAsync(year, cancellationToken).ConfigureAwait(false);

        return AdventureLeaderboardSnapshot.FromDtos(rows);
    }

    private AdventureLeaderboardSnapshot RemoveExcludedUsers(AdventureLeaderboardSnapshot snapshot)
    {
        if (_excludedUserIds.Count == 0)
            return snapshot;

        return snapshot with
        {
            Rows = snapshot.Rows
                .Where(row => !ulong.TryParse(row.UserId, out ulong userId) || !_excludedUserIds.Contains(userId))
                .ToList(),
        };
    }

    private MessageComponent BuildComponents(
        AdventureLeaderboardSnapshot snapshot,
        int year,
        IReadOnlySet<ulong> guildMemberUserIds)
    {
        AdventureLeaderboardViewModel model = AdventureLeaderboardFormatter.Format(
            snapshot,
            year,
            guildMemberUserIds,
            DateTimeOffset.UtcNow);

        return componentBuilder.Build(model);
    }

    private async static Task<IReadOnlySet<ulong>> FindGuildMemberUserIdsAsync(
        IGuild guild,
        AdventureLeaderboardSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        HashSet<ulong> memberIds = new HashSet<ulong>();
        RequestOptions requestOptions = new RequestOptions { CancelToken = cancellationToken };

        foreach (AdventureLeaderboardSnapshotRow row in snapshot.Rows)
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
        AdventureLeaderboardSnapshot snapshot,
        IReadOnlySet<ulong> guildMemberUserIds,
        IReadOnlySet<ulong> excludedUserIds) =>
        $"{snapshot.Hash}:{string.Join(',', guildMemberUserIds.Order())}:{string.Join(',', excludedUserIds.Order())}";

    private async Task<TrackedLeaderboardMessageTarget?> FindTrackedMessageTargetAsync(
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
            return null;
        }

        IUserMessage? message = await channel.GetMessageAsync(
            trackedMessage.MessageId,
            options: new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false) as IUserMessage;

        if (message is null)
        {
            _logger.Warning("Adventure leaderboard message {MessageId} is unavailable.", trackedMessage.MessageId);
            return null;
        }

        return new TrackedLeaderboardMessageTarget(guild, message);
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
                options: new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false);

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

    private sealed record TrackedLeaderboardMessage(
        ulong GuildId,
        ulong ChannelId,
        ulong MessageId,
        int Year,
        string LastRenderHash);

    private sealed record TrackedLeaderboardMessageTarget(
        IGuild Guild,
        IUserMessage Message);
}
