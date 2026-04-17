using RatBot.Domain.Features.Moderation;

namespace RatBot.Application.Features.Moderation.Interfaces;

public interface IModerationService
{
    Task<ErrorOr<AutobannedUser>> RegisterAutobanAsync(
        ulong guildId,
        ulong userId,
        ulong modId,
        CancellationToken ct = default);

    Task<AutobannedUser?> GetAutobanAsync(ulong guildId, ulong userId, CancellationToken ct = default);
}