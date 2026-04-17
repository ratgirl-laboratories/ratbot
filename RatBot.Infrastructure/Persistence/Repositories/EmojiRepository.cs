using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Persistence.Repositories;

public sealed class EmojiRepository(BotDbContext dbContext) : IEmojiRepository
{
    public async Task RecordBatchUsageAsync(IEnumerable<(string Id, int N)> usages, CancellationToken ct = default)
    {
        foreach ((string emojiId, int count) in usages)
        {
            int updatedRowCount = await dbContext
                .Set<EmojiUsageCount>()
                .Where(x => x.EmojiId == emojiId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + count), ct);

            if (updatedRowCount != 0)
                continue;

            dbContext.Set<EmojiUsageCount>().Add(new EmojiUsageCount { EmojiId = emojiId, UsageCount = count });

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                dbContext.ChangeTracker.Clear();

                await dbContext
                    .Set<EmojiUsageCount>()
                    .Where(x => x.EmojiId == emojiId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + count),
                        ct);
            }
        }
    }

    public async Task<ErrorOr<List<EmojiUsageCount>>> GetTopUsageAsync(int limit, CancellationToken ct = default) =>
        await dbContext
            .Set<EmojiUsageCount>()
            .AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ThenBy(x => x.EmojiId)
            .Take(limit)
            .ToListAsync(ct);
}