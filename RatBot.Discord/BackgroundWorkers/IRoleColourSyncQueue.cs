using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace RatBot.Discord.BackgroundWorkers;

public interface IRoleColourSyncQueue
{
    ChannelReader<WorkItem> Reader { get; }

    bool Enqueue(ulong guildId, ulong userId);

    Status GetStatus();

    void OnWorkCompleted(WorkItem item);

    void OnWorkStarted();

    public sealed record Status(int Pending, int InFlight, TimeSpan? Eta);

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct WorkItem(ulong GuildId, ulong UserId);
}
