namespace RatBot.Domain.Features.Quorum;

public readonly record struct QuorumConfigurationId(Guid Value)
{
    public static QuorumConfigurationId New() => new QuorumConfigurationId(Guid.CreateVersion7());
}
