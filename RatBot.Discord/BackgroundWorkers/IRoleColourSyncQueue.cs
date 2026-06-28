using System.Threading.Channels;

namespace RatBot.Discord.BackgroundWorkers;

public interface IRoleColourSyncQueue
{
    ChannelReader<WorkItem> Reader { get; }

    bool Enqueue(ulong guildId, ulong userId);
    ValueTask EnqueueAsync(ulong guildId, ulong userId, CancellationToken ct);

    Status GetStatus();
    void OnWorkCompleted(WorkItem item);

    void OnWorkStarted(WorkItem item);

    public sealed record Status(int Pending, int InFlight, double? PerSecond, TimeSpan? Eta);

    public readonly record struct WorkItem(ulong GuildId, ulong UserId);
}
