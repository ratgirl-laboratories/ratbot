namespace RatBot.Infrastructure.Persistence.Quorum;

internal sealed class QuorumVoterRoleRow
{
    public Guid QuorumConfigurationId { get; init; }
    public ulong RoleId { get; init; }
}
