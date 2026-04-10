namespace RatBot.Infrastructure.Configuration.Quorum;

public sealed class QuorumConfigEntity
{
    public ulong GuildId { get; set; }

    public QuorumConfigType TargetType { get; set; }

    public ulong TargetId { get; set; }

    public double QuorumProportion { get; set; }

    public List<QuorumConfigRoleEntity> Roles { get; set; } = [];
}
