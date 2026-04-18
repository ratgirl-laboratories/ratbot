using Microsoft.Extensions.Options;
using RatBot.Discord.Configuration;
using RatBot.Discord.Gateway;
using Serilog.Events;

namespace RatBot.Discord.Hosting;

public sealed class DiscordBotHostedService : IHostedService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IEnumerable<IDiscordGatewayHandler> _gatewayHandlers;
    private readonly GuildMemberCacheService _guildMemberCacheService;
    private readonly ILogger _logger;
    private readonly DiscordOptions _options;

    public DiscordBotHostedService(
        DiscordSocketClient discordClient,
        InteractionService interactionService,
        IEnumerable<IDiscordGatewayHandler> gatewayHandlers,
        GuildMemberCacheService guildMemberCacheService,
        IOptions<DiscordOptions> options,
        ILogger logger
    )
    {
        _discordClient = discordClient;
        _gatewayHandlers = gatewayHandlers;
        _guildMemberCacheService = guildMemberCacheService;
        _options = options.Value;
        _logger = logger.ForContext<DiscordBotHostedService>();

        _discordClient.Log += message =>
        {
            LogDiscordMessage("Gateway", message);
            return Task.CompletedTask;
        };

        interactionService.Log += message =>
        {
            LogDiscordMessage("Interactions", message);
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordClient.Connected += OnConnectedAsync;
        _discordClient.Disconnected += OnDisconnectedAsync;
        _discordClient.GuildAvailable += OnGuildAvailableAsync;
        _discordClient.GuildMembersDownloaded += OnGuildMembersDownloadedAsync;

        foreach (IDiscordGatewayHandler handler in _gatewayHandlers)
            await handler.InitializeAsync(cancellationToken);

        await _discordClient.LoginAsync(TokenType.Bot, _options.Token);
        await _discordClient.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IDiscordGatewayHandler handler in _gatewayHandlers.Reverse())
            handler.Unsubscribe();

        _discordClient.Connected -= OnConnectedAsync;
        _discordClient.Disconnected -= OnDisconnectedAsync;
        _discordClient.GuildAvailable -= OnGuildAvailableAsync;
        _discordClient.GuildMembersDownloaded -= OnGuildMembersDownloadedAsync;

        await _discordClient.StopAsync();
        await _discordClient.LogoutAsync();
    }

    private Task OnConnectedAsync()
    {
        _logger.Information("Gateway connected.");
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is null)
            _logger.Warning("Gateway disconnected.");
        else
            _logger.Warning(exception, "Gateway disconnected with error.");

        return Task.CompletedTask;
    }

    private Task OnGuildAvailableAsync(SocketGuild guild)
    {
        if (guild.Id != _options.GuildId)
            return Task.CompletedTask;

        _ = _guildMemberCacheService.EnsureGuildMembersDownloadedAsync(guild, "guild_available");

        return Task.CompletedTask;
    }

    private Task OnGuildMembersDownloadedAsync(SocketGuild guild)
    {
        if (guild.Id != _options.GuildId)
            return Task.CompletedTask;

        _logger.Information(
            "Guild member cache populated. GuildId={GuildId}, DownloadedMemberCount={DownloadedMemberCount}, MemberCount={MemberCount}, HasAllMembers={HasAllMembers}",
            guild.Id,
            guild.DownloadedMemberCount,
            guild.MemberCount,
            guild.HasAllMembers
        );

        return Task.CompletedTask;
    }

    private void LogDiscordMessage(string category, LogMessage message)
    {
        LogEventLevel level = message.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            _ => LogEventLevel.Debug
        };

        string source = string.IsNullOrWhiteSpace(message.Source)
            ? "Unknown"
            : message.Source;

        if (message.Exception is not null)
        {
            _logger.Write(
                level,
                message.Exception,
                "[{Category}] {Source}: {Message}",
                category,
                source,
                message.Message);

            return;
        }

        _logger.Write(level, "[{Category}] {Source}: {Message}", category, source, message.Message);
    }
}