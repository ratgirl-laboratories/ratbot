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
        EmojiUsageCount? usageRecord = await _dbContext.EmojiUsageCounts.FindAsync(emojiId);
        if (usageRecord is null)
        {
            _dbContext.EmojiUsageCounts.Add(new EmojiUsageCount { EmojiId = emojiId, UsageCount = 1 });
        }
        else
        {
            usageRecord.UsageCount += 1;
        }

        await _dbContext.SaveChangesAsync();
    }
}
