using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class ReactionEmojiScoreService
{
    private readonly BotDbContext _dbContext;

    public ReactionEmojiScoreService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> GetScoreAsync(string emojiId)
    {
        ReactionEmojiScore? record = await _dbContext.ReactionEmojiScores.FindAsync(emojiId);
        return record?.Score;
    }

    public async Task UpsertAsync(string emojiId, int score)
    {
        ReactionEmojiScore? record = await _dbContext.ReactionEmojiScores.FindAsync(emojiId);
        if (record is null)
        {
            _dbContext.ReactionEmojiScores.Add(new ReactionEmojiScore { EmojiId = emojiId, Score = score });
        }
        else
        {
            record.Score = score;
        }

        await _dbContext.SaveChangesAsync();
    }
}
