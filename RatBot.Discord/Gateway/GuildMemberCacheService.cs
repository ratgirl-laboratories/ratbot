namespace RatBot.Discord.Gateway;

public sealed class GuildMemberCacheService(ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<GuildMemberCacheService>();
    private readonly SemaphoreSlim _warmupLock = new SemaphoreSlim(1, 1);

    public async Task EnsureGuildMembersDownloadedAsync(
        SocketGuild guild,
        string reason,
        CancellationToken ct = default)
    {
        if (guild.HasAllMembers)
        {
            _logger.Debug(
                "Guild member cache is populated. GuildId={GuildId}, DownloadedMemberCount={DownloadedMemberCount}, MemberCount={MemberCount}, Reason={Reason}",
                guild.Id,
                guild.DownloadedMemberCount,
                guild.MemberCount,
                reason
            );

            return;
        }

        if (!await _warmupLock.WaitAsync(0, ct))
        {
            _logger.Debug(
                "Guild member cache download already running. GuildId={GuildId}, Reason={Reason}",
                guild.Id,
                reason);

            return;
        }

        try
        {
            if (guild.HasAllMembers)
                return;

            _logger.Information(
                "Downloading guild members into socket cache. GuildId={GuildId}, DownloadedMemberCount={DownloadedMemberCount}, MemberCount={MemberCount}, Reason={Reason}",
                guild.Id,
                guild.DownloadedMemberCount,
                guild.MemberCount,
                reason
            );

            await guild.DownloadUsersAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to download guild members into socket cache. GuildId={GuildId}, DownloadedMemberCount={DownloadedMemberCount}, MemberCount={MemberCount}, Reason={Reason}",
                guild.Id,
                guild.DownloadedMemberCount,
                guild.MemberCount,
                reason
            );
        }
        finally
        {
            _warmupLock.Release();
        }
    }
}