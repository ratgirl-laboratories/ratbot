namespace RatBot.Domain.Quorum;

public readonly record struct QuorumTarget
{
    private QuorumTarget(ulong guildId, QuorumSettingsType targetType, ulong targetId)
    {
        GuildId = guildId;
        TargetType = targetType;
        TargetId = targetId;
    }

    public ulong GuildId { get; }

    public QuorumSettingsType TargetType { get; }

    public ulong TargetId { get; }

    public static ErrorOr<QuorumTarget> Create(ulong guildId, QuorumSettingsType targetType, ulong targetId)
    {
        if (!Enum.IsDefined(targetType))
            return Error.Validation(description: "Invalid quorum configuration type.");

        return new QuorumTarget(guildId, targetType, targetId);
    }
}
