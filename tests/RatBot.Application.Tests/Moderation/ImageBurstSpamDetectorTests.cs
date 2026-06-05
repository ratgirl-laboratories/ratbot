using RatBot.Application.Moderation;
using Shouldly;

namespace RatBot.Application.Tests.Moderation;

public sealed class ImageBurstSpamDetectorTests
{
    private static readonly DateTimeOffset BaseTimestamp = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Observe_WithFourDistinctChannelsInsideWindow_ReturnsDetection()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = CreateDetector(timeProvider);

        // Act
        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 20, timestamp: BaseTimestamp.AddSeconds(10))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(20))).ShouldBeNull();
        ImageBurstDetection? detection = detector.Observe(CreateMessage(channelId: 40, timestamp: BaseTimestamp.AddSeconds(30)));

        // Assert
        detection.ShouldNotBeNull();
        detection.GuildId.ShouldBe(1UL);
        detection.UserId.ShouldBe(2UL);
        detection.ChannelIds.ShouldBe([10UL, 20UL, 30UL, 40UL]);
        detection.Messages.Count.ShouldBe(4);
    }

    [Test]
    public void Observe_WithRepeatedChannel_DoesNotIncreaseChannelCount()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = CreateDetector(timeProvider);

        // Act
        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp.AddSeconds(5))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 20, timestamp: BaseTimestamp.AddSeconds(10))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(15))).ShouldBeNull();

        // Assert
        detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(20))).ShouldBeNull();
    }

    [Test]
    public void Observe_WithRequiredChannelsButTooFewAttachedMessages_DoesNotReturnDetection()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = new ImageBurstSpamDetector(
            timeProvider,
            new ImageBurstSpamDetectorOptions
            {
                Window = 15,
                DistinctChannelThreshold = 3,
                RequiredAttachedMessageCount = 4,
                HandlingLockDuration = TimeSpan.FromMinutes(5),
            });

        // Act
        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 20, timestamp: BaseTimestamp.AddSeconds(2))).ShouldBeNull();
        ImageBurstDetection? detection =
            detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(4)));

        // Assert
        detection.ShouldBeNull();
    }

    [Test]
    public void Observe_WithThreeAttachedMessagesAcrossThreeChannelsInsideFifteenSeconds_ReturnsDetection()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = new ImageBurstSpamDetector(
            timeProvider,
            new ImageBurstSpamDetectorOptions
            {
                Window = 15,
                DistinctChannelThreshold = 3,
                RequiredAttachedMessageCount = 3,
                HandlingLockDuration = TimeSpan.FromMinutes(5),
            });

        // Act
        detector.Observe(CreateMessage(channelId: 908076393198419988, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 903481102591750184, timestamp: BaseTimestamp.AddMilliseconds(2161))).ShouldBeNull();
        ImageBurstDetection? detection =
            detector.Observe(CreateMessage(channelId: 486841085210132490, timestamp: BaseTimestamp.AddMilliseconds(4362)));

        // Assert
        detection.ShouldNotBeNull();
        detection.ChannelIds.ShouldBe([486841085210132490UL, 903481102591750184UL, 908076393198419988UL]);
        detection.Messages.Count.ShouldBe(3);
    }

    [Test]
    public void Observe_WithOldMessages_PrunesOutsideWindow()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = CreateDetector(timeProvider);

        // Act
        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 20, timestamp: BaseTimestamp.AddSeconds(1))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(2))).ShouldBeNull();

        ImageBurstDetection? detection = detector.Observe(CreateMessage(channelId: 40, timestamp: BaseTimestamp.AddSeconds(46)));

        // Assert
        detection.ShouldBeNull();
    }

    [Test]
    public void Observe_WithSameUserInDifferentGuilds_DoesNotShareWindow()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = CreateDetector(timeProvider);

        // Act
        detector.Observe(CreateMessage(guildId: 1, channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(guildId: 1, channelId: 20, timestamp: BaseTimestamp.AddSeconds(1))).ShouldBeNull();
        detector.Observe(CreateMessage(guildId: 2, channelId: 30, timestamp: BaseTimestamp.AddSeconds(2))).ShouldBeNull();
        detector.Observe(CreateMessage(guildId: 2, channelId: 40, timestamp: BaseTimestamp.AddSeconds(3))).ShouldBeNull();

        // Assert
        detector.Observe(CreateMessage(guildId: 1, channelId: 50, timestamp: BaseTimestamp.AddSeconds(4))).ShouldBeNull();
    }

    [Test]
    public void Observe_AfterDetection_DoesNotTriggerAgainWhileHandlingLockIsActive()
    {
        // Arrange
        TestTimeProvider timeProvider = new TestTimeProvider(BaseTimestamp);
        ImageBurstSpamDetector detector = CreateDetector(timeProvider);

        detector.Observe(CreateMessage(channelId: 10, timestamp: BaseTimestamp)).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 20, timestamp: BaseTimestamp.AddSeconds(1))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 30, timestamp: BaseTimestamp.AddSeconds(2))).ShouldBeNull();
        detector.Observe(CreateMessage(channelId: 40, timestamp: BaseTimestamp.AddSeconds(3))).ShouldNotBeNull();

        // Act
        ImageBurstDetection? detection = detector.Observe(CreateMessage(channelId: 50, timestamp: BaseTimestamp.AddSeconds(4)));

        // Assert
        detection.ShouldBeNull();
    }

    private static ImageBurstSpamDetector CreateDetector(TimeProvider timeProvider) =>
        new ImageBurstSpamDetector(
            timeProvider,
            new ImageBurstSpamDetectorOptions
            {
                Window = 45,
                DistinctChannelThreshold = 4,
                RequiredAttachedMessageCount = 4,
                HandlingLockDuration = TimeSpan.FromMinutes(5),
            });

    private static ImageBurstMessage CreateMessage(
        ulong guildId = 1,
        ulong userId = 2,
        ulong channelId = 10,
        DateTimeOffset? timestamp = null) =>
        new ImageBurstMessage(
            guildId,
            userId,
            channelId,
            timestamp ?? BaseTimestamp,
            [new ImageBurstAttachment("https://cdn.example/a.png"), new ImageBurstAttachment("https://cdn.example/b.png")]);

    private sealed class TestTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
