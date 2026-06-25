namespace RatBot.Domain.Features.Quorum;

public sealed record QuorumConfiguration
{
    private QuorumConfiguration(QuorumConfigurationId id, QuorumScope scope, QuorumProportion proportion, QuorumVoterRoleSet voterRoles)
    {
        Id = id;
        Scope = scope;
        Proportion = proportion;
        VoterRoles = voterRoles;
    }

    public QuorumConfigurationId Id { get; }
    public QuorumScope Scope { get; }
    public QuorumProportion Proportion { get; init; }
    public QuorumVoterRoleSet VoterRoles { get; init; }

    public bool IsComplete => !VoterRoles.IsEmpty;

    public static QuorumConfiguration Create(QuorumScope scope, QuorumProportion proportion) =>
        new QuorumConfiguration(QuorumConfigurationId.New(), scope, proportion, new QuorumVoterRoleSet([]));

    public static QuorumConfiguration Rehydrate(
        QuorumConfigurationId id,
        QuorumScope scope,
        QuorumProportion proportion,
        QuorumVoterRoleSet voterRoles
    ) => new QuorumConfiguration(id, scope, proportion, voterRoles);

    public QuorumConfiguration WithProportion(QuorumProportion proportion) => this with { Proportion = proportion };

    public QuorumConfiguration AddRole(ulong roleId) => this with { VoterRoles = VoterRoles.Add(roleId) };

    public QuorumConfiguration RemoveRole(ulong roleId) => this with { VoterRoles = VoterRoles.Remove(roleId) };
}
