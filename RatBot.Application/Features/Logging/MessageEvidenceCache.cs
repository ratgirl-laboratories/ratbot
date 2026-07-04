using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace RatBot.Application.Features.Logging;

public sealed class MessageEvidenceCache(EvidenceCacheSettings settings)
{
    private readonly Dictionary<ulong, CacheEntry> _entries = new Dictionary<ulong, CacheEntry>();
    private readonly PriorityQueue<QueuedEntry, DateTimeOffset> _evictionQueue = new PriorityQueue<QueuedEntry, DateTimeOffset>();
    private readonly PriorityQueue<QueuedEntry, DateTimeOffset> _expiryQueue = new PriorityQueue<QueuedEntry, DateTimeOffset>();
    private readonly Lock _lock = new Lock();
    private long _totalAttachmentBytes;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                EvictExpired(DateTimeOffset.UtcNow);
                return _entries.Count;
            }
        }
    }

    private static MessageEvidence Empty(ulong messageId) =>
        new MessageEvidence(0, 0, messageId, 0, DateTimeOffset.MinValue, null, ImmutableArray<CachedAttachmentEvidence>.Empty);

    private static long TotalBytes(IEnumerable<CachedAttachmentEvidence> attachments) => attachments.Sum(attachment => attachment.Bytes.LongLength);

    public IReadOnlyDictionary<ulong, MessageEvidence> GetMany(IEnumerable<ulong> messageIds, DateTimeOffset nowUtc)
    {
        Dictionary<ulong, MessageEvidence> results = new Dictionary<ulong, MessageEvidence>();

        lock (_lock)
        {
            EvictExpired(nowUtc);

            foreach (ulong messageId in messageIds)
            {
                if (_entries.TryGetValue(messageId, out CacheEntry? entry))
                    results[messageId] = entry.Evidence;
            }
        }

        return results;
    }

    public void Put(MessageEvidence evidence, DateTimeOffset nowUtc, TimeSpan retentionPeriod)
    {
        settings.Validate();

        if (retentionPeriod <= TimeSpan.Zero)
            throw new InvalidOperationException("Evidence retention period must be positive.");

        ImmutableArray<CachedAttachmentEvidence> attachments = evidence
            .Attachments.Where(attachment => attachment.Bytes.LongLength <= settings.MaxBytesPerAttachment)
            .Take(settings.MaxAttachmentCountPerMessage)
            .Select(attachment => attachment with { Bytes = attachment.Bytes.ToArray() })
            .ToImmutableArray();

        MessageEvidence cachedEvidence = evidence with { Attachments = attachments };
        CacheEntry entry = new CacheEntry(cachedEvidence, nowUtc, nowUtc + retentionPeriod, TotalBytes(attachments));

        lock (_lock)
        {
            EvictExpired(nowUtc);

            if (_entries.Remove(evidence.MessageId, out CacheEntry? previous))
                _totalAttachmentBytes -= previous.AttachmentBytes;

            _entries[evidence.MessageId] = entry;
            _totalAttachmentBytes += entry.AttachmentBytes;
            Enqueue(evidence.MessageId, entry);

            EvictOverflow();
        }
    }

    public void Remove(ulong messageId)
    {
        lock (_lock)
        {
            if (_entries.Remove(messageId, out CacheEntry? entry))
                _totalAttachmentBytes -= entry.AttachmentBytes;
        }
    }

    public bool TryGet(ulong messageId, DateTimeOffset nowUtc, out MessageEvidence evidence)
    {
        lock (_lock)
        {
            EvictExpired(nowUtc);

            if (!_entries.TryGetValue(messageId, out CacheEntry? entry))
            {
                evidence = Empty(messageId);
                return false;
            }

            evidence = entry.Evidence;
            return true;
        }
    }

    private void Enqueue(ulong messageId, CacheEntry entry)
    {
        QueuedEntry queued = new QueuedEntry(messageId, entry.LastTouchedAtUtc, entry.ExpiresAtUtc);
        _evictionQueue.Enqueue(queued, entry.LastTouchedAtUtc);
        _expiryQueue.Enqueue(queued, entry.ExpiresAtUtc);
    }

    private void EvictExpired(DateTimeOffset nowUtc)
    {
        while (_expiryQueue.TryPeek(out QueuedEntry? queued, out DateTimeOffset expiresAtUtc) && queued is not null && expiresAtUtc <= nowUtc)
        {
            _expiryQueue.Dequeue();

            if (!IsCurrent(queued, out CacheEntry? entry))
                continue;

            _entries.Remove(queued.MessageId);
            _totalAttachmentBytes -= entry.AttachmentBytes;
        }
    }

    private void EvictOverflow()
    {
        while (_entries.Count > settings.MaxCachedMessageCount || _totalAttachmentBytes > settings.MaxTotalCachedAttachmentBytes)
        {
            if (!_evictionQueue.TryDequeue(out QueuedEntry? queued, out DateTimeOffset _) || queued is null)
                return;

            if (!IsCurrent(queued, out CacheEntry? entry))
                continue;

            _entries.Remove(queued.MessageId);
            _totalAttachmentBytes -= entry.AttachmentBytes;
        }
    }

    private bool IsCurrent(QueuedEntry queued, [NotNullWhen(true)] out CacheEntry? entry)
    {
        if (!_entries.TryGetValue(queued.MessageId, out entry))
            return false;

        return entry.LastTouchedAtUtc == queued.LastTouchedAtUtc && entry.ExpiresAtUtc == queued.ExpiresAtUtc;
    }

    private sealed record CacheEntry(MessageEvidence Evidence, DateTimeOffset LastTouchedAtUtc, DateTimeOffset ExpiresAtUtc, long AttachmentBytes);

    private sealed record QueuedEntry(ulong MessageId, DateTimeOffset LastTouchedAtUtc, DateTimeOffset ExpiresAtUtc);
}
