using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class UserVirtueService
{
    private readonly BotDbContext _dbContext;

    public UserVirtueService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> AddVirtueDeltaAsync(ulong userId, int delta)
    {
        UserVirtue? userVirtue = await _dbContext.UserVirtues.FindAsync(userId);

        if (userVirtue is null)
        {
            userVirtue = new UserVirtue { UserId = userId, Virtue = delta };
            _dbContext.UserVirtues.Add(userVirtue);
        }
        else
        {
            userVirtue.Virtue += delta;
        }

        await _dbContext.SaveChangesAsync();
        return userVirtue.Virtue;
    }

    public async Task<int> GetVirtueAsync(ulong userId)
    {
        UserVirtue? userVirtue = await _dbContext.UserVirtues.FindAsync(userId);
        return userVirtue?.Virtue ?? 0;
    }

    public async Task<int?> TryGetVirtueAsync(ulong userId)
    {
        UserVirtue? userVirtue = await _dbContext.UserVirtues.FindAsync(userId);
        return userVirtue?.Virtue;
    }

    public Task<List<UserVirtue>> GetTopVirtuesAsync(int limit = 20)
    {
        int clampedLimit = Math.Clamp(limit, 1, 100);

        return _dbContext
            .UserVirtues.AsNoTracking()
            .OrderByDescending(x => x.Virtue)
            .ThenBy(x => x.UserId)
            .Take(clampedLimit)
            .ToListAsync();
    }

    public Task<UserVirtue?> GetLowestVirtueAsync()
    {
        return _dbContext
            .UserVirtues.AsNoTracking()
            .OrderBy(x => x.Virtue)
            .ThenBy(x => x.UserId)
            .FirstOrDefaultAsync();
    }
}
