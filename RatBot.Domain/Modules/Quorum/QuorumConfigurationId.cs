namespace RatBot.Domain.Modules.Quorum;

public readonly record struct QuorumConfigurationId(Guid Value)
{
    public static QuorumConfigurationId New() => new QuorumConfigurationId(Guid.CreateVersion7());
}
