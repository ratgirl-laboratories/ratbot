namespace RatBot.Domain.Moderation;

public sealed class ImageSpamSettings
{
    public const int SingletonId = 1;

    private ImageSpamSettings() { }

    private ImageSpamSettings(int id, int requiredChannelCount, int requiredAttachmentCount, int burstDurationSeconds)
    {
        Id = id;
        RequiredChannelCount = requiredChannelCount;
        RequiredAttachmentCount = requiredAttachmentCount;
        BurstDurationSeconds = burstDurationSeconds;
    }

    public int Id { get; private set; } = SingletonId;

    public int RequiredChannelCount { get; private set; }

    public int RequiredAttachmentCount { get; private set; }

    public int BurstDurationSeconds { get; private set; }

    public static ImageSpamSettings CreateDefault() => new ImageSpamSettings(SingletonId, 4, 2, 45);

    public void Update(int? requiredChannelCount, int? requiredAttachmentCount, int? burstDurationSeconds)
    {
        RequiredChannelCount = requiredChannelCount ?? RequiredChannelCount;
        RequiredAttachmentCount = requiredAttachmentCount ?? RequiredAttachmentCount;
        BurstDurationSeconds = burstDurationSeconds ?? BurstDurationSeconds;
    }
}
