namespace RatBot.Domain.Moderation;

public sealed class ImageSpamSettings
{
    public const int SingletonId = 1;

    private ImageSpamSettings()
    {
    }

    private ImageSpamSettings(int id, int requiredChannelCount, int requiredAttachedMessageCount, int burstDurationSeconds)
    {
        Id = id;
        RequiredChannelCount = requiredChannelCount;
        RequiredAttachedMessageCount = requiredAttachedMessageCount;
        BurstDurationSeconds = burstDurationSeconds;
    }

    public int Id { get; private set; } = SingletonId;

    public int RequiredChannelCount { get; private set; }

    public int RequiredAttachedMessageCount { get; private set; }

    public int BurstDurationSeconds { get; private set; }

    public static ImageSpamSettings CreateDefault() =>
        new ImageSpamSettings(
            SingletonId,
            4,
            4,
            45);

    public void Update(int? requiredChannelCount, int? requiredAttachedMessageCount, int? burstDurationSeconds)
    {
        RequiredChannelCount = requiredChannelCount ?? RequiredChannelCount;
        RequiredAttachedMessageCount = requiredAttachedMessageCount ?? RequiredAttachedMessageCount;
        BurstDurationSeconds = burstDurationSeconds ?? BurstDurationSeconds;
    }
}
