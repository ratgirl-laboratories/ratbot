using RatBot.Domain.Moderation;

namespace RatBot.Application.Moderation;

public interface IAutobannedUserRepository
{
    Task<AutobannedUser?> GetAsync(ulong guildId, ulong userId, CancellationToken ct = default);
    Task AddAsync(AutobannedUser user, CancellationToken ct = default);
}