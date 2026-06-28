namespace RatBot.Application.Quorum;

public interface IQuorumSettingsRepository
{
    Task<ErrorOr<Deleted>> DeleteAsync(QuorumTarget target);
    Task<ErrorOr<QuorumSettings>> GetAsync(QuorumTarget target);

    Task<ErrorOr<Success>> UpsertAsync(QuorumSettings config);
}
