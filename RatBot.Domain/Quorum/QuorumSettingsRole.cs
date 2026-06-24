namespace RatBot.Domain.Quorum;

public record QuorumSettingsRole
{
    public ulong GuildId { get; init; }

    public QuorumSettingsType TargetType { get; init; }

    public ulong TargetId { get; init; }

    public ulong Id { get; init; }
}
