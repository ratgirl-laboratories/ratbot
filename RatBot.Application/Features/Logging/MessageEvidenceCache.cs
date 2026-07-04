using System.Collections.Immutable;

namespace RatBot.Application.Features.Logging;

public sealed class MessageEvidenceCache(EvidenceCacheSettings settings)
{
    private readonly Dictionary<ulong, CacheEntry> _entries = new Dictionary<ulong, CacheEntry>();
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

            EvictOverflow();
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

    public void Remove(ulong messageId)
    {
        lock (_lock)
        {
            if (_entries.Remove(messageId, out CacheEntry? entry))
                _totalAttachmentBytes -= entry.AttachmentBytes;
        }
    }

    private static MessageEvidence Empty(ulong messageId) =>
        new MessageEvidence(0, 0, messageId, 0, DateTimeOffset.MinValue, null, ImmutableArray<CachedAttachmentEvidence>.Empty);

    private static long TotalBytes(IEnumerable<CachedAttachmentEvidence> attachments) => attachments.Sum(attachment => attachment.Bytes.LongLength);

    private void EvictExpired(DateTimeOffset nowUtc)
    {
        foreach (KeyValuePair<ulong, CacheEntry> pair in _entries.Where(pair => pair.Value.ExpiresAtUtc <= nowUtc).ToArray())
        {
            _entries.Remove(pair.Key);
            _totalAttachmentBytes -= pair.Value.AttachmentBytes;
        }
    }

    private void EvictOverflow()
    {
        while (_entries.Count > settings.MaxCachedMessageCount || _totalAttachmentBytes > settings.MaxTotalCachedAttachmentBytes)
        {
            KeyValuePair<ulong, CacheEntry> oldest = _entries.OrderBy(pair => pair.Value.LastTouchedAtUtc).First();
            _entries.Remove(oldest.Key);
            _totalAttachmentBytes -= oldest.Value.AttachmentBytes;
        }
    }

    private sealed record CacheEntry(MessageEvidence Evidence, DateTimeOffset LastTouchedAtUtc, DateTimeOffset ExpiresAtUtc, long AttachmentBytes);
}
