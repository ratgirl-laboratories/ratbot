using Microsoft.EntityFrameworkCore;
using RatBot.Application.Common.Interfaces;
using RatBot.Domain.Emoji;

namespace RatBot.Application.Reactions;

public sealed class ReactionUsageTracker(
    IEmojiRepository emojiRepository,
    ITrackedEmojiCatalog trackedEmojiCatalog,
    ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<ReactionUsageTracker>();

    public async Task RecordBatchUsageAsync(IEnumerable<ulong> emojiIds, CancellationToken ct = default)
    {
        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(out IReadOnlyCollection<ulong> trackedEmojiIds))
            return;

        HashSet<ulong> trackedEmojiIdSet = trackedEmojiIds.ToHashSet();

        await PruneUntrackedEmojiAsync(trackedEmojiIds, ct).ConfigureAwait(false);

        List<(ulong Id, int N)> usages = emojiIds
            .Where(trackedEmojiIdSet.Contains)
            .GroupBy(x => x)
            .Select(g => (EmojiId: g.Key, Count: g.Count()))
            .ToList();

        foreach ((ulong emojiId, int count) in usages)
        {
            int updatedRowCount = await emojiRepository.EmojiUsageCounts
                .Where(x => x.EmojiId == emojiId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ReactionUsageCount,
                        x => x.ReactionUsageCount + count),
                    ct)
                .ConfigureAwait(false);

            if (updatedRowCount != 0)
                continue;

            emojiRepository.EmojiUsageCounts.Add(
                new EmojiUsageCount
                {
                    EmojiId = emojiId,
                    ReactionUsageCount = count,
                    MessageUsageCount = 0,
                });

            await emojiRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach ((ulong Id, int N) usage in usages)
            _logger.Verbose("Recorded {EmojiUsageCount} usages for emoji {EmojiId}.", usage.N, usage.Id);
    }

    public async Task<ErrorOr<List<EmojiUsageCount>>> GetTopUsageAsync(int limit = 25, CancellationToken ct = default)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);
        ErrorOr<EmojiUsagePage> pageResult = await GetUsagePageAsync(1, clampedLimit, ct).ConfigureAwait(false);

        return pageResult.IsError
            ? pageResult.Errors
            : pageResult.Value.Items.ToList();
    }

    public async Task<ErrorOr<EmojiUsagePage>> GetUsagePageAsync(
        int page,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        int clampedPageSize = Math.Clamp(pageSize, 1, 100);

        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(out IReadOnlyCollection<ulong> trackedEmojiIds))
            return Error.Unexpected(description: "Tracked guild emoji are not available yet.");

        await PruneUntrackedEmojiAsync(trackedEmojiIds, ct).ConfigureAwait(false);

        IQueryable<EmojiUsageCount> query = emojiRepository.EmojiUsageCounts
            .AsNoTracking()
            .Where(x => trackedEmojiIds.Contains(x.EmojiId));

        int totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        if (totalCount == 0)
            return Error.NotFound(description: "No emoji usage has been recorded yet.");

        int totalPages = (int)Math.Ceiling((double)totalCount / clampedPageSize);
        int clampedPage = Math.Clamp(page, 1, totalPages);

        List<EmojiUsageCount> topUsage = await emojiRepository.EmojiUsageCounts
            .AsNoTracking()
            .Where(x => trackedEmojiIds.Contains(x.EmojiId))
            .OrderByDescending(x => x.ReactionUsageCount + x.MessageUsageCount)
            .ThenBy(x => x.EmojiId)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new EmojiUsagePage(topUsage, clampedPage, totalPages, totalCount);
    }

    private Task<int> PruneUntrackedEmojiAsync(IReadOnlyCollection<ulong> trackedEmojiIds, CancellationToken ct) =>
        emojiRepository.EmojiUsageCounts
            .Where(x => !trackedEmojiIds.Contains(x.EmojiId))
            .ExecuteDeleteAsync(ct);
}