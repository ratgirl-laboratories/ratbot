using Microsoft.EntityFrameworkCore;
using RatBot.Application.Persistence;

namespace RatBot.Application.Features.Emoji;

public sealed class EmojiAnalyticsService(IBotDataContext dbContext, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<EmojiAnalyticsService>();

    public async Task RecordUsageAsync(string emojiId, CancellationToken ct = default)
    {
        await RecordBatchUsageAsync([emojiId], ct);
    }

    public async Task RecordBatchUsageAsync(IEnumerable<string> emojiIds, CancellationToken ct = default)
    {
        Dictionary<string, int> counts = emojiIds
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach ((string emojiId, int count) in counts)
        {
            int updatedRowCount = await dbContext
                .EmojiUsageCounts
                .Where(x => x.EmojiId == emojiId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + count), ct);

            if (updatedRowCount == 0)
            {
                dbContext.EmojiUsageCounts.Add(new EmojiUsageCount { EmojiId = emojiId, UsageCount = count });

                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    dbContext.ChangeTracker.Clear();

                    updatedRowCount = await dbContext
                        .EmojiUsageCounts.Where(x => x.EmojiId == emojiId)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + count),
                            ct);

                    if (updatedRowCount == 0)
                        throw;
                }
            }

            _logger.Debug("Recorded {EmojiUsageCount} usages for emoji {EmojiId}.", count, emojiId);
        }
    }

    public Task<List<EmojiUsageCount>> GetTopUsageAsync(int limit = 25, CancellationToken ct = default)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);

        return dbContext
            .EmojiUsageCounts.AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ThenBy(x => x.EmojiId)
            .Take(clampedLimit)
            .ToListAsync(ct);
    }
}