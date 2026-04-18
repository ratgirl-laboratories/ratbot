using Microsoft.Extensions.Options;
using RatBot.Discord.Configuration;
using RatBot.Discord.Gateway;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class GuildMemberCacheBackgroundWorker(
    DiscordSocketClient discordClient,
    GuildMemberCacheService memberCacheService,
    IOptions<DiscordOptions> options,
    ILogger logger
) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<GuildMemberCacheBackgroundWorker>();
    private readonly DiscordOptions _options = options.Value;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(_options.MemberCacheRefreshIntervalMinutes);

        _logger.Information(
            "Guild member cache background worker started. GuildId={GuildId}, IntervalMinutes={IntervalMinutes}",
            _options.GuildId,
            _options.MemberCacheRefreshIntervalMinutes
        );

        try
        {
            using PeriodicTimer timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await CheckGuildMemberCacheAsync(stoppingToken);
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

    private async Task CheckGuildMemberCacheAsync(CancellationToken ct)
    {
        SocketGuild? guild = discordClient.GetGuild(_options.GuildId);

        if (guild is null)
        {
            _logger.Debug("Configured guild is not currently available. GuildId={GuildId}", _options.GuildId);
            return;
        }

        await memberCacheService.EnsureGuildMembersDownloadedAsync(guild, "periodic_check", ct);
    }
}