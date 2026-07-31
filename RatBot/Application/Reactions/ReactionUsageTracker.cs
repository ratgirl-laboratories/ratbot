using RatBot.Application.Common.Interfaces;
using RatBot.Application.Features.EmojiAnalytics;
using RatBot.Domain.Emoji;

namespace RatBot.Application.Reactions;

public sealed class ReactionUsageTracker(IEmojiUsageStore emojiUsageStore, ITrackedEmojiCatalog trackedEmojiCatalog, ILogger logger)
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

        int totalCount = await emojiUsageStore.CountTrackedUsageAsync(guildId, trackedEmojiIds, ct).ConfigureAwait(false);

        if (totalCount == 0)
            return Error.NotFound(description: "No emoji usage has been recorded yet.");

        int totalPages = (int)Math.Ceiling((double)totalCount / clampedPageSize);
        int clampedPage = Math.Clamp(page, 1, totalPages);

        IReadOnlyList<EmojiUsageCount> topUsage = await emojiUsageStore
            .GetTrackedUsagePageAsync(guildId, trackedEmojiIds, (clampedPage - 1) * clampedPageSize, clampedPageSize, ct)
            .ConfigureAwait(false);

        return new EmojiUsagePage(topUsage, clampedPage, totalPages, totalCount);
    }

    public async Task RecordBatchUsageAsync(ulong guildId, IEnumerable<ulong> emojiIds, CancellationToken ct = default)
    {
        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(guildId, out IReadOnlyCollection<ulong> trackedEmojiIds))
            return;

        HashSet<ulong> trackedEmojiIdSet = trackedEmojiIds.ToHashSet();

        Dictionary<ulong, int> usages = emojiIds.Where(trackedEmojiIdSet.Contains).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        await emojiUsageStore.RecordReactionUsageAsync(guildId, usages, trackedEmojiIds, ct).ConfigureAwait(false);

        foreach ((ulong emojiId, int count) in usages)
            _logger.Verbose("Recorded {EmojiUsageCount} usages for emoji {EmojiId}.", count, emojiId);
    }
}
