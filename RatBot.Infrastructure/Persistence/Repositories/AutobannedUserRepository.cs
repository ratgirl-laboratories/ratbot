using RatBot.Application.Moderation;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Persistence.Repositories;

public sealed class AutobannedUserRepository(BotDbContext dbContext) : IAutobannedUserRepository
{
    public Task<AutobannedUser?> GetAsync(
        ulong guildId,
        ulong userId,
        CancellationToken ct = default) =>
        dbContext.AutobannedUsers.SingleOrDefaultAsync(
            user => user.GuildId == guildId && user.BannedUser == userId,
            ct);

    public async Task AddAsync(AutobannedUser user, CancellationToken ct = default)
    {
        await dbContext.AutobannedUsers.AddAsync(user, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}