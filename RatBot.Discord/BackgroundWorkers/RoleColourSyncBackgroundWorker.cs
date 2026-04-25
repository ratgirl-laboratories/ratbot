using System.Threading.Channels;
using RatBot.Discord.Handlers;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class RoleColourSyncBackgroundWorker(
    IRoleColourSyncQueue queue,
    DiscordSocketClient discordClient,
    IRoleColourReconciler reconciler,
    ILogger logger)
    : BackgroundService
{
    private static readonly int DefaultConcurrency = Environment.ProcessorCount >> 1;
    private readonly ILogger _log = logger.ForContext<RoleColourSyncBackgroundWorker>();

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> workers = new List<Task>(DefaultConcurrency);

        for (int i = 0; i < DefaultConcurrency; i++)
            workers.Add(Task.Run(() => RunConsumerAsync(queue.Reader, stoppingToken), stoppingToken));

        return Task.WhenAll(workers);
    }

    private async Task RunConsumerAsync(ChannelReader<IRoleColourSyncQueue.WorkItem> reader, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            IRoleColourSyncQueue.WorkItem item;

            try
            {
                item = await reader.ReadAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            queue.OnWorkStarted(item);

            try
            {
                SocketGuild? guild = discordClient.GetGuild(item.GuildId);

                if (guild is null)
                {
                    _log.Debug(
                        "role_colour_sync guild_missing guild_id={GuildId} user_id={UserId}",
                        item.GuildId,
                        item.UserId);

                    continue;
                }

                await reconciler.ReconcileMemberAsync(guild, item.UserId, ct);
            }
            catch (Exception ex)
            {
                _log.Error(
                    ex,
                    "role_colour_sync failed guild_id={GuildId} user_id={UserId}",
                    item.GuildId,
                    item.UserId);
                // best-effort; continue
            }
            finally
            {
                queue.OnWorkCompleted(item);
            }
        }
    }
}