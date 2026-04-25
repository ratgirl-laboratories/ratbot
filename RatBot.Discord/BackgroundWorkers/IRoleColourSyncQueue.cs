using System.Threading.Channels;

namespace RatBot.Discord.BackgroundWorkers;

public interface IRoleColourSyncQueue
{

    ChannelReader<WorkItem> Reader { get; }

    bool Enqueue(ulong guildId, ulong userId);
    ValueTask EnqueueAsync(ulong guildId, ulong userId, CancellationToken ct);

    void OnWorkStarted(WorkItem item);
    void OnWorkCompleted(WorkItem item);

    Status GetStatus();
    public readonly record struct WorkItem(ulong GuildId, ulong UserId);

    public sealed record Status(int Pending, int InFlight, double? PerSecond, TimeSpan? Eta);
}