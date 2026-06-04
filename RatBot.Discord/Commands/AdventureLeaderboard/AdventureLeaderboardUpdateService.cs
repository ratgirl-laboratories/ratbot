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
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger = logger.ForContext<AdventureLeaderboardUpdateService>();
    private readonly AdventureLeaderboardOptions _options = options.Value;
    private TrackedLeaderboardMessage? _trackedMessage;

    public async Task<IUserMessage> CreateLeaderboardMessageAsync(
        ITextChannel channel,
        int year,
        CancellationToken cancellationToken)
    {
        AdventureLeaderboardSnapshot snapshot = await FetchSnapshotAsync(year, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<ulong> guildMemberUserIds =
            await FindGuildMemberUserIdsAsync(channel.Guild, snapshot, cancellationToken).ConfigureAwait(false);
        string renderHash = BuildRenderHash(snapshot, guildMemberUserIds);
        MessageComponent components = BuildComponents(snapshot, year, guildMemberUserIds);

        IUserMessage message = await channel.SendMessageAsync(
            allowedMentions: AllowedMentions.None,
            components: components,
            flags: MessageFlags.ComponentsV2).ConfigureAwait(false);

        TrackedLeaderboardMessage? previousMessage;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
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

            AdventureLeaderboardSnapshot snapshot = await FetchSnapshotAsync(
                _trackedMessage.Year,
                cancellationToken).ConfigureAwait(false);

            TrackedLeaderboardMessageTarget? target =
                await FindTrackedMessageTargetAsync(_trackedMessage, cancellationToken).ConfigureAwait(false);

            if (target is null)
                return;

            IReadOnlySet<ulong> guildMemberUserIds =
                await FindGuildMemberUserIdsAsync(target.Guild, snapshot, cancellationToken).ConfigureAwait(false);
            string renderHash = BuildRenderHash(snapshot, guildMemberUserIds);

            if (string.Equals(renderHash, _trackedMessage.LastRenderHash, StringComparison.Ordinal))
                return;

            MessageComponent components = BuildComponents(snapshot, _trackedMessage.Year, guildMemberUserIds);

            await target.Message.ModifyAsync(
                properties =>
                {
                    properties.Components = components;
                    properties.AllowedMentions = AllowedMentions.None;
                },
                new RequestOptions { CancelToken = cancellationToken }).ConfigureAwait(false);

            _trackedMessage = _trackedMessage with { LastRenderHash = renderHash };
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

    private async Task<AdventureLeaderboardSnapshot> FetchSnapshotAsync(int year, CancellationToken cancellationToken)
    {
        IReadOnlyList<AdventureLeaderboardEntryDto> rows =
            await client.GetLeaderboardAsync(year, cancellationToken).ConfigureAwait(false);

        return AdventureLeaderboardSnapshot.FromDtos(rows);
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
        IReadOnlySet<ulong> guildMemberUserIds) =>
        $"{snapshot.Hash}:{string.Join(',', guildMemberUserIds.Order())}";

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
