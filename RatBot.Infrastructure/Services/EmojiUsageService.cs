using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class EmojiUsageService
{
    private readonly BotDbContext _dbContext;

    public EmojiUsageService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task IncrementUsageAsync(string emojiId)
    {
        int updatedRowCount = await _dbContext
            .EmojiUsageCounts.Where(x => x.EmojiId == emojiId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + 1));

        if (updatedRowCount != 0)
            return;

        _dbContext.EmojiUsageCounts.Add(new EmojiUsageCount { EmojiId = emojiId, UsageCount = 1 });

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // A concurrent insert won the race; increment the now-existing row.
            _dbContext.ChangeTracker.Clear();
            await _dbContext
                .EmojiUsageCounts.Where(x => x.EmojiId == emojiId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsageCount, x => x.UsageCount + 1));
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
    {
        string message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }

    public Task<List<EmojiUsageCount>> GetTopUsageAsync(int limit = 25)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);

        return _dbContext
            .EmojiUsageCounts.AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .ThenBy(x => x.EmojiId)
            .Take(clampedLimit)
            .ToListAsync();
    }
}
