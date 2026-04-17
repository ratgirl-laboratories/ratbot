using RatBot.Domain.Emoji;

namespace RatBot.Application.Emoji;

public sealed class EmojiAnalyticsService(IEmojiRepository emojiRepository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<EmojiAnalyticsService>();

    public async Task RecordUsageAsync(string emojiId, CancellationToken ct = default) =>
        await RecordBatchUsageAsync([emojiId], ct);

    public async Task RecordBatchUsageAsync(IEnumerable<string> emojiIds, CancellationToken ct = default)
    {
        List<(string Id, int N)> usages = emojiIds
            .GroupBy(x => x)
            .Select(g => (EmojiId: g.Key, Count: g.Count()))
            .ToList();

        await emojiRepository.RecordBatchUsageAsync(usages, ct);

        foreach ((string Id, int N) usage in usages)
            _logger.Debug("Recorded {EmojiUsageCount} usages for emoji {EmojiId}.", usage.N, usage.Id);
    }

    public async Task<ErrorOr<List<EmojiUsageCount>>> GetTopUsageAsync(int limit = 25, CancellationToken ct = default)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);

        ErrorOr<List<EmojiUsageCount>> topUsageResult = await emojiRepository.GetTopUsageAsync(clampedLimit, ct);

        if (topUsageResult.IsError)
            return topUsageResult.Errors;

        List<EmojiUsageCount> topUsage = topUsageResult.Value;

        if (topUsage.Count == 0)
            return Error.NotFound(description: "No emoji usage has been recorded yet.");

        return topUsage;
    }
}