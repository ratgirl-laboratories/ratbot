using Microsoft.EntityFrameworkCore;
using RatBot.Application.Common.Interfaces;
using RatBot.Domain.Emoji;

namespace RatBot.Application.Reactions;

public sealed class ReactionUsageTracker(IEmojiRepository emojiRepository, ITrackedEmojiCatalog trackedEmojiCatalog, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<ReactionUsageTracker>();

    public async Task<ErrorOr<List<EmojiUsageCount>>> GetTopUsageAsync(ulong guildId, int limit = 25, CancellationToken ct = default)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);
        ErrorOr<EmojiUsagePage> pageResult = await GetUsagePageAsync(guildId, 1, clampedLimit, ct).ConfigureAwait(false);

        return pageResult.IsError ? pageResult.Errors : pageResult.Value.Items.ToList();
    }

    public async Task<ErrorOr<EmojiUsagePage>> GetUsagePageAsync(ulong guildId, int page, int pageSize = 25, CancellationToken ct = default)
    {
        int clampedPageSize = Math.Clamp(pageSize, 1, 100);

        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(guildId, out IReadOnlyCollection<ulong> trackedEmojiIds))
            return Error.Unexpected(description: "Tracked guild emoji are not available yet.");

        await PruneUntrackedEmojiAsync(guildId, trackedEmojiIds, ct).ConfigureAwait(false);

        IQueryable<EmojiUsageCount> query = emojiRepository
            .EmojiUsageCounts.AsNoTracking()
            .Where(x => x.GuildId == guildId && trackedEmojiIds.Contains(x.EmojiId));

        int totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        if (totalCount == 0)
            return Error.NotFound(description: "No emoji usage has been recorded yet.");

        int totalPages = (int)Math.Ceiling((double)totalCount / clampedPageSize);
        int clampedPage = Math.Clamp(page, 1, totalPages);

        List<EmojiUsageCount> topUsage = await emojiRepository
            .EmojiUsageCounts.AsNoTracking()
            .Where(x => x.GuildId == guildId && trackedEmojiIds.Contains(x.EmojiId))
            .OrderByDescending(x => x.ReactionUsageCount + x.MessageUsageCount)
            .ThenBy(x => x.EmojiId)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new EmojiUsagePage(topUsage, clampedPage, totalPages, totalCount);
    }

    public async Task RecordBatchUsageAsync(ulong guildId, IEnumerable<ulong> emojiIds, CancellationToken ct = default)
    {
        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(guildId, out IReadOnlyCollection<ulong> trackedEmojiIds))
            return;

        HashSet<ulong> trackedEmojiIdSet = trackedEmojiIds.ToHashSet();

        await PruneUntrackedEmojiAsync(guildId, trackedEmojiIds, ct).ConfigureAwait(false);

        List<(ulong Id, int N)> usages = emojiIds
            .Where(trackedEmojiIdSet.Contains)
            .GroupBy(x => x)
            .Select(g => (EmojiId: g.Key, Count: g.Count()))
            .ToList();

        foreach ((ulong emojiId, int count) in usages)
        {
            int updatedRowCount = await emojiRepository
                .EmojiUsageCounts.Where(x => x.GuildId == guildId && x.EmojiId == emojiId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReactionUsageCount, x => x.ReactionUsageCount + count), ct)
                .ConfigureAwait(false);

            if (updatedRowCount != 0)
                continue;

            emojiRepository.EmojiUsageCounts.Add(
                new EmojiUsageCount
                {
                    GuildId = guildId,
                    EmojiId = emojiId,
                    ReactionUsageCount = count,
                    MessageUsageCount = 0,
                }
            );

            await emojiRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach ((ulong Id, int N) usage in usages)
            _logger.Verbose("Recorded {EmojiUsageCount} usages for emoji {EmojiId}.", usage.N, usage.Id);
    }

    private Task<int> PruneUntrackedEmojiAsync(ulong guildId, IReadOnlyCollection<ulong> trackedEmojiIds, CancellationToken ct) =>
        emojiRepository.EmojiUsageCounts.Where(x => x.GuildId == guildId && !trackedEmojiIds.Contains(x.EmojiId)).ExecuteDeleteAsync(ct);
}
