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

    public async Task<Dictionary<ulong, int>> GetVirtuesAsync(IEnumerable<ulong> userIds)
    {
        List<ulong> ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<ulong, int>();

        return await _dbContext
            .UserVirtues.Where(x => ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.Virtue);
    }
}
