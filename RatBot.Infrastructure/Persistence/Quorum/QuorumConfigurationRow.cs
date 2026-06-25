namespace RatBot.Infrastructure.Persistence.Quorum;

internal sealed class QuorumConfigurationRow
{
    public Guid Id { get; init; }

    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public string ChannelKind { get; init; } = null!;

    public decimal Proportion { get; set; }

    public ICollection<QuorumVoterRoleRow> VoterRoles { get; } = [];
}