namespace RatBot.Domain.Features.Quorum;

public sealed record QuorumVoterRoleSet
{
    public QuorumVoterRoleSet(IEnumerable<ulong> roleIds)
    {
        RoleIds = roleIds.ToImmutableHashSet();
    }

    public ImmutableHashSet<ulong> RoleIds { get; }

    public bool IsEmpty => RoleIds.IsEmpty;

    public QuorumVoterRoleSet Add(ulong roleId) => new QuorumVoterRoleSet(RoleIds.Add(roleId));

    public QuorumVoterRoleSet Remove(ulong roleId) => new QuorumVoterRoleSet(RoleIds.Remove(roleId));
}
