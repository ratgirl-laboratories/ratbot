using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class UserScoreService
{
    private readonly BotDbContext _dbContext;

    public UserScoreService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> AddDeltaAsync(ulong userId, int delta)
    {
        UserScore? userScore = await _dbContext.UserScores.FindAsync(userId);

        if (userScore is null)
        {
            userScore = new UserScore { UserId = userId, Score = delta };
            _dbContext.UserScores.Add(userScore);
        }
        else
        {
            userScore.Score += delta;
        }

        await _dbContext.SaveChangesAsync();
        return userScore.Score;
    }

    public async Task<int> GetScoreAsync(ulong userId)
    {
        UserScore? userScore = await _dbContext.UserScores.FindAsync(userId);
        return userScore?.Score ?? 0;
    }

    public async Task<Dictionary<ulong, int>> GetScoresAsync(IEnumerable<ulong> userIds)
    {
        List<ulong> ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<ulong, int>();

        return await _dbContext.UserScores
            .Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.Score);
    }
}
