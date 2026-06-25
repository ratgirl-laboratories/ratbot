using System.Collections.Immutable;
using RatBot.Domain.Modules.Quorum;

namespace RatBot.Application.Modules.Quorum;

public interface IQuorumMemberSource
{
    Task<ErrorOr<int>> CountEligibleVotersAsync(QuorumScope scope, ImmutableHashSet<ulong> roleIds, CancellationToken ct);
}