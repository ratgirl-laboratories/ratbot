using RatBot.Domain.Features.Moderation;

namespace RatBot.Application.Features.Moderation.Interfaces;

public interface IAutobannedUserRepository
{
    Task<AutobannedUser?> GetAsync(ulong guildId, ulong userId, CancellationToken ct = default);
    Task AddAsync(AutobannedUser user, CancellationToken ct = default);
}
