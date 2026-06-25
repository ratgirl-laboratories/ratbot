using RatBot.Domain.Features.Quorum;

namespace RatBot.Application.Features.Quorum;

public interface IQuorumConfigurationStore
{
    Task<ErrorOr<QuorumConfiguration>> GetAsync(QuorumScope scope, CancellationToken ct);

    Task<ErrorOr<QuorumRegistration>> RegisterAsync(QuorumScope scope, QuorumProportion proportion, CancellationToken ct);

    Task<ErrorOr<QuorumConfiguration>> SaveAsync(QuorumConfiguration configuration, CancellationToken ct);

    Task<ErrorOr<Deleted>> DeleteAsync(QuorumScope scope, CancellationToken ct);
}
