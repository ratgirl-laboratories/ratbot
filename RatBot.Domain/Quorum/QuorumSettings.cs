namespace RatBot.Domain.Quorum;

/// <summary>
///     Represents quorum behaviour for a guild target.
/// </summary>
public sealed class QuorumSettings
{
    private readonly List<QuorumSettingsRole> _roles = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuorumSettings" /> class.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="targetType">The configuration target type.</param>
    /// <param name="targetId">The configuration target identifier.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    public QuorumSettings(ulong guildId, QuorumSettingsType targetType, ulong targetId, double quorumProportion)
    {
        GuildId = guildId;
        TargetType = targetType;
        TargetId = targetId;
        QuorumProportion = quorumProportion;
    }

    /// <summary>
    ///     Gets the guild identifier.
    /// </summary>
    public ulong GuildId { get; }

    /// <summary>
    ///     Gets the configuration target type.
    /// </summary>
    public QuorumSettingsType TargetType { get; }

    /// <summary>
    ///     Gets the configuration target identifier.
    /// </summary>
    public ulong TargetId { get; }

    /// <summary>
    ///     Gets the role rows used for persistence.
    /// </summary>
    public IReadOnlyCollection<QuorumSettingsRole> Roles => _roles;

    /// <summary>
    ///     Gets the quorum proportion.
    /// </summary>
    public double QuorumProportion { get; }

    public void ReplaceRoles(IEnumerable<ulong> roleIds)
    {
        _roles.Clear();

        _roles.AddRange(
            roleIds
                .Distinct()
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