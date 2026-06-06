namespace RatBot.Application.Moderation;

public sealed record ImageBurstSpamDetectorOptions
{
    public int Window { get; init; } = 45;

    public int DistinctChannelThreshold { get; init; } = 4;

    public int RequiredAttachmentCount { get; init; } = 2;

    public TimeSpan HandlingLockDuration { get; init; } = TimeSpan.FromMinutes(3);
}
