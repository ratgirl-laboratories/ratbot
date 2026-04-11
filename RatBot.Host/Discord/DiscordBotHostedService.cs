using Discord.Interactions;
using Microsoft.Extensions.Options;
using RatBot.Host.Configuration;

namespace RatBot.Host.Discord;

public sealed class DiscordBotHostedService : IHostedService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly DiscordInteractionHandler _interactionHandler;
    private readonly EmojiReactionGatewayHandler _emojiReactionHandler;
    private readonly ILogger _logger;
    private readonly DiscordOptions _options;

    public DiscordBotHostedService(
        DiscordSocketClient discordClient,
        InteractionService interactionService,
        DiscordInteractionHandler interactionHandler,
        EmojiReactionGatewayHandler emojiReactionHandler,
        IOptions<DiscordOptions> options,
        ILogger logger)
    {
        _discordClient = discordClient;
        _interactionHandler = interactionHandler;
        _emojiReactionHandler = emojiReactionHandler;
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

        await _interactionHandler.InitializeAsync(cancellationToken);
        _emojiReactionHandler.Subscribe();

        await _discordClient.LoginAsync(TokenType.Bot, _options.Token);
        await _discordClient.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _emojiReactionHandler.Unsubscribe();
        _interactionHandler.Unsubscribe();

        _discordClient.Connected -= OnConnectedAsync;
        _discordClient.Disconnected -= OnDisconnectedAsync;

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

        string source = string.IsNullOrWhiteSpace(message.Source) ? "Unknown" : message.Source;

        if (message.Exception is not null)
        {
            _logger.Write(level, message.Exception, "[{Category}] {Source}: {Message}", category, source, message.Message);
            return;
        }

        _logger.Write(level, "[{Category}] {Source}: {Message}", category, source, message.Message);
    }
}
