namespace RatBot.Application.Quorum;

public interface IQuorumSettingsRepository
{
    Task<ErrorOr<QuorumSettings>> GetAsync(ulong guildId, QuorumSettingsType targetType, ulong targetId);

    Task<ErrorOr<Success>> UpsertAsync(QuorumSettings config);

    Task<ErrorOr<Deleted>> DeleteAsync(ulong guildId, QuorumSettingsType targetType, ulong targetId);
}