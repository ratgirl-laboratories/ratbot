namespace RatBot.Application.Moderation;

public sealed record ImageBurstSpamDetectorOptions
{
    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(45);

    public int DistinctChannelThreshold { get; init; } = 4;

    public TimeSpan HandlingLockDuration { get; init; } = TimeSpan.FromMinutes(5);
}
