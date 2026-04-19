namespace RatBot.Application.Quorum;

public interface IQuorumSettingsRepository
{
    Task<ErrorOr<QuorumSettings>> GetAsync(QuorumTarget target);

    Task<ErrorOr<Success>> UpsertAsync(QuorumSettings config);

    Task<ErrorOr<Deleted>> DeleteAsync(QuorumTarget target);
}
