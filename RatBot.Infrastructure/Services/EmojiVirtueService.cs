using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class EmojiVirtueService
{
    private readonly BotDbContext _dbContext;

    public EmojiVirtueService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> GetVirtueAsync(string emojiId)
    {
        EmojiVirtue? record = await _dbContext.EmojiVirtues.FindAsync(emojiId);
        return record?.Virtue;
    }

    public async Task UpsertVirtueAsync(string emojiId, int virtue)
    {
        EmojiVirtue? record = await _dbContext.EmojiVirtues.FindAsync(emojiId);
        if (record is null)
        {
            _dbContext.EmojiVirtues.Add(new EmojiVirtue { EmojiId = emojiId, Virtue = virtue });
        }
        else
        {
            record.Virtue = virtue;
        }

        await _dbContext.SaveChangesAsync();
    }
}
