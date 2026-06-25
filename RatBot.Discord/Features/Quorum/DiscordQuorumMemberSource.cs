using System.Collections.Immutable;
using RatBot.Application.Features.Quorum;
using RatBot.Domain.Features.Quorum;

namespace RatBot.Discord.Features.Quorum;

/// <inheritdoc />
public class DiscordQuorumMemberSource : IQuorumMemberSource
{
    /// <inheritdoc />
    public Task<ErrorOr<int>> CountEligibleVotersAsync(QuorumScope scope, ImmutableHashSet<ulong> roleIds, CancellationToken ct) =>
        throw new NotImplementedException();
}
