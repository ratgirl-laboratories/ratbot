using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class RoleColourSyncQueue : IRoleColourSyncQueue
{
    private const int DefaultCapacity = 50_000;
    private readonly Channel<IRoleColourSyncQueue.WorkItem> _channel;

    public RoleColourSyncQueue()
        : this(DefaultCapacity) { }

    internal RoleColourSyncQueue(int capacity)
    {
        _channel = Channel.CreateBounded<IRoleColourSyncQueue.WorkItem>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            }
        );
    }

    private readonly ConcurrentQueue<DateTimeOffset> _completedTimestamps = new ConcurrentQueue<DateTimeOffset>();

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), byte> _dedupe =
        new ConcurrentDictionary<(ulong GuildId, ulong UserId), byte>();

    private int _inFlight;

    private int _pending;

    public ChannelReader<IRoleColourSyncQueue.WorkItem> Reader => _channel.Reader;

    public bool Enqueue(ulong guildId, ulong userId)
    {
        (ulong, ulong) key = (guildId, userId);

        if (!_dedupe.TryAdd(key, 0))
            return false; // already queued or in-flight

        Interlocked.Increment(ref _pending);
        IRoleColourSyncQueue.WorkItem item = new IRoleColourSyncQueue.WorkItem(guildId, userId);
        bool ok = _channel.Writer.TryWrite(item);

        if (ok)
            return true;

        RollBackEnqueue(key);
        return false;
    }

    public IRoleColourSyncQueue.Status GetStatus() =>
        new IRoleColourSyncQueue.Status(Volatile.Read(ref _pending), Volatile.Read(ref _inFlight), ComputeThroughputAndEta());

    public void OnWorkCompleted(IRoleColourSyncQueue.WorkItem item)
    {
        Interlocked.Decrement(ref _inFlight);
        _dedupe.TryRemove((item.GuildId, item.UserId), out _);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _completedTimestamps.Enqueue(now);

        // Trim to a recent window (~200 samples or 2 minutes)
        while (_completedTimestamps.Count > 200)
            _completedTimestamps.TryDequeue(out _);

        // Also drop items older than 2 minutes
        while (_completedTimestamps.TryPeek(out DateTimeOffset head) && now - head > TimeSpan.FromMinutes(2))
            _completedTimestamps.TryDequeue(out _);
    }

    public void OnWorkStarted()
    {
        Interlocked.Decrement(ref _pending);
        Interlocked.Increment(ref _inFlight);
    }

    private void RollBackEnqueue((ulong GuildId, ulong UserId) key)
    {
        Interlocked.Decrement(ref _pending);
        _dedupe.TryRemove(key, out _);
    }

    private TimeSpan? ComputeThroughputAndEta()
    {
        DateTimeOffset[] points = _completedTimestamps.ToArray();

        if (points.Length < 2)
            return null;

        Array.Sort(points);
        DateTimeOffset first = points[0];
        DateTimeOffset last = points[^1];
        double seconds = (last - first).TotalSeconds;

        if (seconds <= 0.001)
            return null;

        double rate = (points.Length - 1) / seconds; // items per second
        int remaining = Math.Max(0, Volatile.Read(ref _pending) + Volatile.Read(ref _inFlight));

        TimeSpan eta = rate > 0 ? TimeSpan.FromSeconds(remaining / rate) : TimeSpan.Zero;

        return eta;
    }
}
