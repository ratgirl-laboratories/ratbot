namespace RatBot.Domain.Quorum;

/// <summary>
///     Represents quorum behaviour for a guild target.
/// </summary>
public sealed class QuorumSettings
{
    private readonly List<QuorumSettingsRole> _roles = [];

    private QuorumSettings()
    {
    }

    private QuorumSettings(QuorumTarget target)
    {
        GuildId = target.GuildId;
        TargetType = target.TargetType;
        TargetId = target.TargetId;
    }

    public ulong GuildId { get; private set; }

    public QuorumSettingsType TargetType { get; private set; }

    public ulong TargetId { get; private set; }

    public IReadOnlyCollection<QuorumSettingsRole> Roles => _roles;

    public double Proportion { get; private set; }

    public static ErrorOr<QuorumSettings> Create(
        QuorumTarget target,
        IEnumerable<ulong> roleIds,
        Proportion proportion)
    {
        ErrorOr<QuorumTarget> targetResult = QuorumTarget.Create(target.GuildId, target.TargetType, target.TargetId);

        if (targetResult.IsError)
            return targetResult.Errors;

        QuorumSettings settings = new QuorumSettings(targetResult.Value);
        ErrorOr<Success> updateResult = settings.Update(roleIds, proportion);

        return updateResult.IsError
            ? updateResult.Errors
            : settings;
    }

    public ErrorOr<Success> Update(IEnumerable<ulong> roleIds, Proportion proportion)
    {
        ErrorOr<Proportion> quorumProportionResult =
            Quorum.Proportion.Create(proportion.Value);

        if (quorumProportionResult.IsError)
            return quorumProportionResult.Errors;

        ulong[] canonicalRoleIds = roleIds.Distinct().ToArray();

        if (canonicalRoleIds.Length == 0)
            return Error.Validation(description: "At least one role must be provided.");

        Proportion = quorumProportionResult.Value.Value;
        ReplaceRoles(canonicalRoleIds);

        return Result.Success;
    }

    private void ReplaceRoles(IEnumerable<ulong> roleIds)
    {
        _roles.Clear();

        _roles.AddRange(
            roleIds
                .Select(roleId => new QuorumSettingsRole
                {
                    Id = roleId,
                    GuildId = GuildId,
                    TargetType = TargetType,
                    TargetId = TargetId
                })
        );
    }
}
