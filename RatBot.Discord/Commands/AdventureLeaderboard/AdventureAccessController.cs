using System.Runtime.CompilerServices;
using Discord.Rest;
using Microsoft.EntityFrameworkCore;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureAccessController(
    IDbContextFactory<BotDbContext> dbContextFactory,
    IOptions<AdventureLeaderboardOptions> options,
    ILogger logger
)
{
    private static readonly AllowedMentions UserMentionsOnly = new AllowedMentions(AllowedMentionTypes.Users);
    private readonly ILogger _logger = logger.ForContext<AdventureAccessController>();
    private readonly AdventureLeaderboardOptions _options = options.Value;

    private static async Task<HashSet<ulong>> GetThreadMemberIdsAsync(IThreadChannel thread, RequestOptions options)
    {
        switch (thread)
        {
            case SocketThreadChannel socketThread:
            {
                IReadOnlyCollection<SocketThreadUser> users = await socketThread.GetUsersAsync(options).ConfigureAwait(false);

                return users.Select(user => user.Id).ToHashSet();
            }
            case RestThreadChannel restThread:
            {
                HashSet<ulong> memberIds = new HashSet<ulong>();

                ConfiguredCancelableAsyncEnumerable<IReadOnlyCollection<RestThreadUser>> userAsyncEnumerator = restThread
                    .GetThreadUsersAsync(100, options)
                    .WithCancellation(options.CancelToken)
                    .ConfigureAwait(false);

                await foreach (IReadOnlyCollection<RestThreadUser> users in userAsyncEnumerator)
                foreach (RestThreadUser user in users)
                    memberIds.Add(user.Id);

                return memberIds;
            }
            default:
                return new HashSet<ulong>();
        }
    }

    private static async Task<ImmutableDictionary<ulong, IGuildUser>> ResolveGuildMembersAsync(
        SocketGuild guild,
        AdventureEntrySnapshot snapshot,
        RequestOptions options
    )
    {
        ImmutableDictionary<ulong, IGuildUser>.Builder members = ImmutableDictionary.CreateBuilder<ulong, IGuildUser>();

        foreach (AdventureEntryRow row in snapshot.Rows)
        {
            if (!ulong.TryParse(row.UserId, out ulong userId))
                continue;

            IGuildUser? member =
                guild.GetUser(userId) ?? await ((IGuild)guild).GetUserAsync(userId, CacheMode.AllowDownload, options).ConfigureAwait(false);

            if (member is not null)
                members.Add(userId, member);
        }

        return members.ToImmutable();
    }

    public async Task UpdateAccessGrantsAsync(SocketGuild guild, AdventureEntrySnapshot snapshot, CancellationToken ct)
    {
        ImmutableArray<AdventureForumThreadLink> links;

        try
        {
            await using BotDbContext dbContext = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            links = (
                await dbContext.AdventureForumThreadLinks.AsNoTracking().OrderBy(link => link.ScorePartIndex).ToListAsync(ct).ConfigureAwait(false)
            ).ToImmutableArray();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not load adventure forum thread links; skipping access sync.");
            return;
        }

        if (links.Length == 0)
        {
            _logger.Debug("Adventure forum access sync skipped because no score-part threads are configured.");
            return;
        }

        RequestOptions requestOptions = new RequestOptions { CancelToken = ct };

        ImmutableDictionary<ulong, IGuildUser> guildMembers = await ResolveGuildMembersAsync(guild, snapshot, requestOptions).ConfigureAwait(false);

        ImmutableDictionary<int, ulong> threadIdsByScorePart = links.ToImmutableDictionary(link => link.ScorePartIndex, link => link.ThreadId);

        ImmutableHashSet<ulong> adventurerUserIds = guildMembers
            .Values.Where(user => user.RoleIds.Contains(_options.AdventurerRoleId))
            .Select(user => user.Id)
            .ToImmutableHashSet();

        AdventureAccessGrants grants = AdventureGrantManager.GenerateAdventureAccessGrants(snapshot, threadIdsByScorePart, adventurerUserIds);

        int attempted = 0;
        int alreadyPresent = 0;
        int failures = 0;
        int notificationFailures = 0;
        int skippedThreads = 0;

        foreach (IGrouping<ulong, AdventureAccessGrant> accessGrants in grants.Grants.GroupBy(grant => grant.ThreadId))
        {
            IThreadChannel? thread = await ResolveThreadAsync(guild, accessGrants.Key, requestOptions).ConfigureAwait(false);

            if (thread is null)
            {
                skippedThreads++;
                continue;
            }

            if (!await PrepareThreadAsync(thread, requestOptions).ConfigureAwait(false))
            {
                skippedThreads++;
                continue;
            }

            HashSet<ulong> currentMemberIds = await GetThreadMemberIdsAsync(thread, requestOptions).ConfigureAwait(false);

            foreach (AdventureAccessGrant grant in accessGrants)
            {
                if (currentMemberIds.Contains(grant.UserId))
                {
                    alreadyPresent++;
                    continue;
                }

                if (!guildMembers.TryGetValue(grant.UserId, out IGuildUser? user))
                {
                    failures++;
                    continue;
                }

                attempted++;

                try
                {
                    await thread.AddUserAsync(user, requestOptions).ConfigureAwait(false);
                    currentMemberIds.Add(user.Id);

                    if (!await SendAdventureThreadWelcomeAsync(thread, user, requestOptions).ConfigureAwait(false))
                        notificationFailures++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures++;

                    _logger.Warning(
                        ex,
                        "Failed to add user {UserId} to adventure forum thread {ThreadId} for score part {ScorePartIndex}.",
                        grant.UserId,
                        grant.ThreadId,
                        grant.ScorePartIndex
                    );
                }
            }
        }

        _logger.Information(
            "Adventure forum access sync completed. ThreadLinks={ThreadLinkCount} GuildUsersConsidered={GuildUserCount} AdventurersEligible={AdventurerCount} GrantsAttempted={GrantsAttempted} GrantsAlreadyPresent={GrantsAlreadyPresent} Failures={Failures} NotificationFailures={NotificationFailures} ThreadsSkipped={ThreadsSkipped}.",
            links.Length,
            guildMembers.Count,
            adventurerUserIds.Count,
            attempted,
            alreadyPresent,
            failures,
            notificationFailures,
            skippedThreads
        );
    }

    private async Task<bool> PrepareThreadAsync(IThreadChannel thread, RequestOptions requestOptions)
    {
        try
        {
            if (thread.IsArchived)
                await thread.ModifyAsync(properties => properties.Archived = false, requestOptions).ConfigureAwait(false);

            if (!thread.HasJoined)
                await thread.JoinAsync(requestOptions).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Adventure forum thread {ThreadId} is inaccessible or could not be unarchived.", thread.Id);

            return false;
        }
    }

    private async Task<IThreadChannel?> ResolveThreadAsync(SocketGuild guild, ulong threadId, RequestOptions options)
    {
        SocketThreadChannel? thread = guild.GetThreadChannel(threadId);

        if (thread is not null)
            return thread;

        IThreadChannel? downloadedThread = await ((IGuild)guild)
            .GetThreadChannelAsync(threadId, CacheMode.AllowDownload, options)
            .ConfigureAwait(false);

        if (downloadedThread is not null)
            return downloadedThread;

        _logger.Warning("Adventure forum thread {ThreadId} is missing or unavailable.", threadId);
        return null;
    }

    private async Task<bool> SendAdventureThreadWelcomeAsync(IThreadChannel thread, IGuildUser user, RequestOptions requestOptions)
    {
        try
        {
            await thread
                .SendMessageAsync(
                    $"Congratulations, Adventurer {MentionUtils.MentionUser(user.Id)}!",
                    allowedMentions: UserMentionsOnly,
                    options: requestOptions
                )
                .ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (requestOptions.CancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to send adventure forum thread welcome message to user {UserId} in thread {ThreadId}.", user.Id, thread.Id);

            return false;
        }
    }
}
