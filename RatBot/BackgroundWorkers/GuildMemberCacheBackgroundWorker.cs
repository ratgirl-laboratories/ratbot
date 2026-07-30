using RatBot.Configuration;
using RatBot.Gateway;

namespace RatBot.BackgroundWorkers;

public sealed class GuildMemberCacheBackgroundWorker(
    DiscordSocketClient discordClient,
    GuildMemberCacheService memberCacheService,
    IOptions<DiscordOptions> options,
    ILogger logger
) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<GuildMemberCacheBackgroundWorker>();
    private readonly DiscordOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(_options.MemberCacheRefreshIntervalMinutes);

        _logger.Information(
            "Guild member cache background worker started. IntervalMinutes={IntervalMinutes}",
            _options.MemberCacheRefreshIntervalMinutes
        );

        try
        {
            using PeriodicTimer timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await CheckGuildMemberCachesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Guild member cache background worker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Guild member cache background worker encountered an error.");
        }
    }

    private async Task CheckGuildMemberCachesAsync(CancellationToken ct)
    {
        await ProcessGuildsAsync(
            discordClient.Guilds,
            guild => guild.Id,
            guild => memberCacheService.EnsureGuildMembersDownloadedAsync(guild, "periodic_check", ct),
            _logger,
            ct
        );
    }

    internal static async Task ProcessGuildsAsync<TGuild>(
        IEnumerable<TGuild> guilds,
        Func<TGuild, ulong> getGuildId,
        Func<TGuild, Task> processGuildAsync,
        ILogger logger,
        CancellationToken ct
    )
    {
        foreach (TGuild guild in guilds)
        {
            ulong guildId = getGuildId(guild);

            try
            {
                await processGuildAsync(guild);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Guild member cache refresh failed for guild {GuildId}.", guildId);
            }
        }
    }
}
