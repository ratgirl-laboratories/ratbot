using RatBot.Discord.Handlers;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class RoleColourReconciliationBackgroundWorker(
    DiscordSocketClient discordClient,
    IOptions<DiscordOptions> options,
    RoleColourReconciler reconciler,
    ILogger logger
) : BackgroundService
{
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(30);
    private readonly ILogger _logger = logger.ForContext<RoleColourReconciliationBackgroundWorker>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(ReconciliationInterval);

        do
        {
            SocketGuild? guild = discordClient.GetGuild(options.Value.GuildId);

            if (guild is null)
            {
                _logger.Warning("Role colour reconciliation skipped because guild {GuildId} is unavailable.", options.Value.GuildId);
            }
            else
            {
                try
                {
                    int changed = await reconciler.ReconcileGuildAsync(guild, stoppingToken);

                    _logger.Information(
                        "Role colour reconciliation completed for guild {GuildId}; changed {ChangedCount} members.",
                        guild.Id,
                        changed
                    );
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Role colour reconciliation failed for guild {GuildId}; retrying next interval.", guild.Id);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
