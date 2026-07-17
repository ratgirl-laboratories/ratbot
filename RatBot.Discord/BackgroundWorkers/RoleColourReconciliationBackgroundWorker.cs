using Microsoft.EntityFrameworkCore;
using RatBot.Discord.Handlers;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class RoleColourReconciliationBackgroundWorker(
    DiscordSocketClient discordClient,
    IDbContextFactory<BotDbContext> dbContextFactory,
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
            await ReconcileConfiguredGuildsAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReconcileConfiguredGuildsAsync(CancellationToken ct)
    {
        await using BotDbContext db = await dbContextFactory.CreateDbContextAsync(ct);
        ulong[] configuredGuildIds = await db.RoleColourOptions.AsNoTracking().Select(option => option.GuildId).Distinct().ToArrayAsync(ct);

        await ProcessConfiguredGuildsAsync(
            configuredGuildIds,
            guildId => discordClient.GetGuild(guildId) is not null,
            async guildId =>
            {
                SocketGuild guild = discordClient.GetGuild(guildId)!;
                return await reconciler.ReconcileGuildAsync(guild, ct);
            },
            _logger,
            ct
        );
    }

    internal static async Task ProcessConfiguredGuildsAsync(
        IEnumerable<ulong> configuredGuildIds,
        Func<ulong, bool> guildAvailable,
        Func<ulong, Task<int>> reconcileGuildAsync,
        ILogger logger,
        CancellationToken ct
    )
    {
        foreach (ulong guildId in configuredGuildIds)
        {
            if (!guildAvailable(guildId))
            {
                logger.Warning("Role colour reconciliation skipped because guild {GuildId} is unavailable.", guildId);
                continue;
            }

            try
            {
                int changed = await reconcileGuildAsync(guildId);

                logger.Information("Role colour reconciliation completed for guild {GuildId}; changed {ChangedCount} members.", guildId, changed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Role colour reconciliation failed for guild {GuildId}; retrying next interval.", guildId);
            }
        }
    }
}
