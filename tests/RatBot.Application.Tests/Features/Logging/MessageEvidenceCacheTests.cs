using RatBot.Application.Features.Logging;
using Shouldly;

namespace RatBot.Application.Tests.Features.Logging;

[TestFixture]
public sealed class MessageEvidenceCacheTests
{
    [Test]
    public void TryGet_WhenEvidenceExpired_ReturnsFalse()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(Settings());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Put(Evidence(1, attachmentSize: 10), now, TimeSpan.FromSeconds(5));

        bool found = cache.TryGet(1, now.AddSeconds(6), out MessageEvidence _);

        found.ShouldBeFalse();
        cache.Count.ShouldBe(0);
    }

    [Test]
    public void Put_EnforcesMessageCountAndAttachmentLimits()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(
            Settings(maxCachedMessageCount: 2, maxAttachmentCountPerMessage: 1, maxBytesPerAttachment: 10, maxTotalCachedAttachmentBytes: 100)
        );
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Put(Evidence(1, attachmentSize: 10), now, TimeSpan.FromMinutes(5));
        cache.Put(Evidence(2, attachmentSize: 10), now.AddSeconds(1), TimeSpan.FromMinutes(5));
        cache.Put(Evidence(3, attachmentSize: 20), now.AddSeconds(2), TimeSpan.FromMinutes(5));

        cache.TryGet(1, now.AddSeconds(2), out MessageEvidence _).ShouldBeFalse();
        cache.TryGet(2, now.AddSeconds(2), out MessageEvidence second).ShouldBeTrue();
        cache.TryGet(3, now.AddSeconds(2), out MessageEvidence third).ShouldBeTrue();
        second.Attachments.Count.ShouldBe(1);
        third.Attachments.Count.ShouldBe(0);
    }

    [Test]
    public void Put_WhenEntryIsRefreshed_DoesNotEvictItFromStaleQueueEntry()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(Settings(maxCachedMessageCount: 2));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Put(Evidence(1, attachmentSize: 10), now, TimeSpan.FromMinutes(5));
        cache.Put(Evidence(2, attachmentSize: 10), now.AddSeconds(1), TimeSpan.FromMinutes(5));
        cache.Put(Evidence(1, attachmentSize: 10), now.AddSeconds(2), TimeSpan.FromMinutes(5));
        cache.Put(Evidence(3, attachmentSize: 10), now.AddSeconds(3), TimeSpan.FromMinutes(5));

        cache.TryGet(1, now.AddSeconds(3), out MessageEvidence _).ShouldBeTrue();
        cache.TryGet(2, now.AddSeconds(3), out MessageEvidence _).ShouldBeFalse();
        cache.TryGet(3, now.AddSeconds(3), out MessageEvidence _).ShouldBeTrue();
    }

    [Test]
    public void Put_EnforcesTotalAttachmentBytes()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(
            Settings(maxCachedMessageCount: 10, maxAttachmentCountPerMessage: 1, maxBytesPerAttachment: 100, maxTotalCachedAttachmentBytes: 15)
        );
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Put(Evidence(1, attachmentSize: 10), now, TimeSpan.FromMinutes(5));
        cache.Put(Evidence(2, attachmentSize: 10), now.AddSeconds(1), TimeSpan.FromMinutes(5));

        cache.TryGet(1, now.AddSeconds(1), out MessageEvidence _).ShouldBeFalse();
        cache.TryGet(2, now.AddSeconds(1), out MessageEvidence _).ShouldBeTrue();
    }

    [Test]
    public void Put_WhenCapacityIsSustained_EvictsOldestCurrentEntries()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(Settings(maxCachedMessageCount: 3));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (ulong messageId = 1; messageId <= 10; messageId++)
            cache.Put(Evidence(messageId, attachmentSize: 10), now.AddSeconds(messageId), TimeSpan.FromMinutes(5));

        cache.Count.ShouldBe(3);

        for (ulong messageId = 1; messageId <= 7; messageId++)
            cache.TryGet(messageId, now.AddSeconds(10), out MessageEvidence _).ShouldBeFalse();

        for (ulong messageId = 8; messageId <= 10; messageId++)
            cache.TryGet(messageId, now.AddSeconds(10), out MessageEvidence _).ShouldBeTrue();
    }

    [Test]
    public void Put_UsesRetentionPeriodFromInsertion()
    {
        MessageEvidenceCache cache = new MessageEvidenceCache(Settings());
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Put(Evidence(1, attachmentSize: 10), now, TimeSpan.FromSeconds(5));
        cache.Put(Evidence(2, attachmentSize: 10), now, TimeSpan.FromSeconds(20));

        cache.TryGet(1, now.AddSeconds(6), out MessageEvidence _).ShouldBeFalse();
        cache.TryGet(2, now.AddSeconds(6), out MessageEvidence _).ShouldBeTrue();
    }

    private static MessageEvidence Evidence(ulong messageId, int attachmentSize) =>
        new MessageEvidence(
            10,
            20,
            messageId,
            30,
            DateTimeOffset.UtcNow,
            "content",
            new[] { new CachedAttachmentEvidence(1, new byte[attachmentSize], "application/octet-stream") }
        );

    private static EvidenceCacheSettings Settings(
        int maxCachedMessageCount = 10,
        int maxAttachmentCountPerMessage = 2,
        long maxBytesPerAttachment = 1024,
        long maxTotalCachedAttachmentBytes = 1024
    ) => new EvidenceCacheSettings(maxCachedMessageCount, maxAttachmentCountPerMessage, maxBytesPerAttachment, maxTotalCachedAttachmentBytes);
}
