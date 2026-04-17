using System.Text.Json.Serialization;

namespace RatBot.Domain.Features.Quorum;

/// <summary>
///     Represents quorum behaviour for a guild target.
/// </summary>
public sealed record QuorumSettings
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="QuorumSettings" /> class.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="targetType">The configuration target type.</param>
    /// <param name="targetId">The configuration target identifier.</param>
    /// <param name="roleIds">The role identifiers used for quorum counting.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    [JsonConstructor]
    public QuorumSettings(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        ulong[] roleIds,
        double quorumProportion)
    {
        GuildId = RequireGuildId(guildId);
        TargetType = RequireTargetType(targetType);
        TargetId = RequireDiscordId(targetId, nameof(targetId));
        RoleIds = RequireRoleIds(roleIds).ToArray();
        QuorumProportion = RequireQuorumProportion(quorumProportion);
    }

    // EF binds this constructor from QuorumConfigs columns and role IDs are loaded separately.
    private QuorumSettings(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        double quorumProportion)
        : this(guildId, targetType, targetId, [], quorumProportion)
    {
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
    ///     Gets the role identifiers used for quorum counting.
    /// </summary>
    public ulong[] RoleIds { get; }

    /// <summary>
    ///     Gets the quorum proportion.
    /// </summary>
    public double QuorumProportion { get; }

    /// <summary>
    ///     Creates a new config using a single role identifier.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="targetType">The configuration target type.</param>
    /// <param name="targetId">The configuration target identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    /// <returns>The created config.</returns>
    public static QuorumSettings Create(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        ulong roleId,
        double quorumProportion) => Create(guildId, targetType, targetId, [roleId], quorumProportion);

    /// <summary>
    ///     Creates a new config.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="targetType">The configuration target type.</param>
    /// <param name="targetId">The configuration target identifier.</param>
    /// <param name="roleIds">The role identifiers.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    /// <returns>The created config.</returns>
    public static QuorumSettings Create(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        ulong[] roleIds,
        double quorumProportion) =>
        new QuorumSettings(guildId, targetType, targetId, roleIds, quorumProportion);

    private static ulong RequireGuildId(ulong value) =>
        value == 0
            ? throw new ArgumentOutOfRangeException(nameof(value), "Discord identifiers must be non-zero.")
            : value;

    private static ulong RequireDiscordId(ulong value, string paramName) =>
        value == 0
            ? throw new ArgumentOutOfRangeException(paramName, "Discord identifiers must be non-zero.")
            : value;

    private static QuorumSettingsType RequireTargetType(QuorumSettingsType targetType) =>
        !Enum.IsDefined(targetType)
            ? throw new ArgumentOutOfRangeException(nameof(targetType), "Invalid quorum configuration type.")
            : targetType;

    private static ulong[] RequireRoleIds(IEnumerable<ulong> roleIds)
    {
        ulong[] validatedRoleIds = roleIds
            .Select(roleId => RequireDiscordId(roleId, nameof(roleIds)))
            .Distinct()
            .ToArray();

        return validatedRoleIds.Length == 0
            ? throw new ArgumentOutOfRangeException(nameof(roleIds), "At least one role identifier must be provided.")
            : validatedRoleIds;
    }

    private static double RequireQuorumProportion(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Quorum proportion must be a finite number.");

        if (value is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Quorum proportion must be greater than 0 and at most 1.");

        return value;
    }

    /// <summary>
    ///     Creates a replacement config using a single role identifier.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    /// <returns>The replacement config.</returns>
    public QuorumSettings Reconfigure(ulong roleId, double quorumProportion) =>
        Reconfigure([roleId], quorumProportion);

    /// <summary>
    ///     Creates a replacement config.
    /// </summary>
    /// <param name="roleIds">The role identifiers.</param>
    /// <param name="quorumProportion">The quorum proportion.</param>
    /// <returns>The replacement config.</returns>
    public QuorumSettings Reconfigure(ulong[] roleIds, double quorumProportion) =>
        new QuorumSettings(GuildId, TargetType, TargetId, roleIds.ToArray(), quorumProportion);
}
