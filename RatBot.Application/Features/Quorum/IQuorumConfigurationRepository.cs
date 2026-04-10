using RatBot.Domain.Enums;
using RatBot.Domain.Features.Quorum;

namespace RatBot.Application.Features.Quorum;

public interface IQuorumConfigurationRepository
{
    Task<QuorumConfig?> GetAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        CancellationToken ct = default);

    Task<bool> UpsertAsync(QuorumConfig config, CancellationToken ct = default);

    Task<bool> DeleteAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        CancellationToken ct = default);
}