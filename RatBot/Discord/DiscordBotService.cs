using System.Reflection;
using Discord.Interactions;
using Serilog.Events;
using IResult = Discord.Interactions.IResult;

namespace RatBot.Discord;

/// <summary>
/// Coordinates Discord client lifecycle, interaction module registration, and interaction execution.
/// </summary>
public sealed class DiscordBotService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordBotService"/> class.
    /// </summary>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="interactionService">The interaction service.</param>
    /// <param name="services">The root service provider.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public DiscordBotService(
        DiscordSocketClient discordClient,
        InteractionService interactionService,
        IServiceProvider services,
        IConfiguration config,
        ILogger logger
    )
    {
        _discordClient = discordClient;
        _interactionService = interactionService;
        _services = services;
        _config = config;
        _logger = logger.ForContext<DiscordBotService>();
    }

    /// <summary>
    /// Starts the Discord bot connection and interaction handlers.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when startup work has finished.</returns>
    public async Task StartAsync(CancellationToken ct)
    {
        _discordClient.Log += msg =>
        {
            LogDiscordMessage("Gateway", msg);
            return Task.CompletedTask;
        };

        _discordClient.Connected += () =>
        {
            _logger.Information("Gateway connected.");
            return Task.CompletedTask;
        };

        _discordClient.Disconnected += ex =>
        {
            if (ex is null)
                _logger.Warning("Gateway disconnected.");
            else
                _logger.Warning(ex, "Gateway disconnected with error.");

            return Task.CompletedTask;
        };

        Assembly slashCommandsAssembly = Assembly.Load("RatBot.Interactions");
        Assembly mainAssembly = Assembly.GetExecutingAssembly();

        await _interactionService.AddModulesAsync(mainAssembly, _services);
        await _interactionService.AddModulesAsync(slashCommandsAssembly, _services);
        _logger.Information("Registered {InteractionModuleCount} interaction modules.", _interactionService.Modules.Count);

        _discordClient.Ready += async () =>
        {
            try
            {
                string guildIdStr = _config["Discord:GuildId"] ?? throw new InvalidOperationException("Discord guild id missing");
                if (!ulong.TryParse(guildIdStr, out ulong guildId))
                    throw new InvalidOperationException("Discord guild id is invalid");

                // Overwrite guild commands to match current modules (deleteMissing=true)
                await _interactionService.RegisterCommandsToGuildAsync(guildId, deleteMissing: true);
                _logger.Information("Slash commands registered to guild {GuildId}.", guildId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to register slash commands.");
            }
        };

        _discordClient.InteractionCreated += HandleInteractionAsync;
        _discordClient.ReactionAdded += HandleReactionAddedAsync;
        _discordClient.ReactionRemoved += HandleReactionRemovedAsync;
        _discordClient.ReactionsCleared += HandleReactionsClearedAsync;
        _discordClient.ReactionsRemovedForEmote += HandleReactionsRemovedForEmoteAsync;

        // Forward InteractionService logs to console to aid troubleshooting
        _interactionService.Log += msg =>
        {
            LogDiscordMessage("Interactions", msg);
            return Task.CompletedTask;
        };

        string tokenValue = _config["Discord:Token"] ?? throw new InvalidOperationException("Discord token missing");

        await _discordClient.LoginAsync(TokenType.Bot, tokenValue);
        await _discordClient.StartAsync();
    }

    /// <summary>
    /// Stops the Discord bot connection.
    /// </summary>
    /// <returns>A task that completes when shutdown has finished.</returns>
    public async Task StopAsync()
    {
        await _discordClient.StopAsync();
        await _discordClient.LogoutAsync();
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (interaction.Type is not (InteractionType.ApplicationCommand or InteractionType.ModalSubmit))
            return;

        try
        {
            IInteractionContext context = new SocketInteractionContext(_discordClient, interaction);
            IResult result = await _interactionService.ExecuteCommandAsync(context, _services);

            if (result.IsSuccess)
                return;

            _logger.Warning("Interaction command failed. Error={Error} Reason={Reason}", result.Error, result.ErrorReason);

            string reason = string.IsNullOrWhiteSpace(result.ErrorReason) ? "Command execution failed." : result.ErrorReason;

            if (!interaction.HasResponded)
                await interaction.RespondAsync($"Command failed: {reason}", ephemeral: true);
            else
                await interaction.FollowupAsync($"Command failed: {reason}", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Interaction execution failed.");

            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("An error occurred executing that command.", ephemeral: true);
                else
                    await interaction.FollowupAsync("An error occurred executing that command.", ephemeral: true);
            }
            catch (Exception followupEx)
            {
                _logger.Warning(followupEx, "Failed to send interaction error follow-up.");
            }
        }
    }

    private async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction
    )
    {
        await LogReactionEventAsync("added", reaction);
    }

    private async Task HandleReactionRemovedAsync(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction
    )
    {
        await LogReactionEventAsync("removed", reaction);
    }

    private Task HandleReactionsClearedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            _logger.ForContext("ReactionEventType", "cleared_all").Information("Discord reaction event recorded.");

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private Task HandleReactionsRemovedForEmoteAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, IEmote emote)
    {
        try
        {
            LogReactionEmoteClearEvent(emote);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
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
            _ => LogEventLevel.Debug,
        };

        string source = string.IsNullOrWhiteSpace(message.Source) ? "Unknown" : message.Source;
        if (message.Exception is not null)
        {
            _logger.Write(level, message.Exception, "[{Category}] {Source}: {Message}", category, source, message.Message);

            return;
        }

        _logger.Write(level, "[{Category}] {Source}: {Message}", category, source, message.Message);
    }

    private Task LogReactionEventAsync(string reactionEventType, SocketReaction reaction)
    {
        try
        {
            string emojiName = reaction.Emote.Name;
            ulong? emojiId = reaction.Emote is Emote customEmote ? customEmote.Id : null;

            _logger
                .ForContext("ReactionEventType", reactionEventType)
                .ForContext("EmojiName", emojiName)
                .ForContext("EmojiId", emojiId)
                .ForContext("IsCustomEmoji", emojiId.HasValue)
                .Information("Discord reaction event recorded.");

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private void LogReactionEmoteClearEvent(IEmote emote)
    {
        ulong? emojiId = emote is Emote customEmote ? customEmote.Id : null;

        _logger
            .ForContext("ReactionEventType", "cleared_emote")
            .ForContext("EmojiName", emote.Name)
            .ForContext("EmojiId", emojiId)
            .ForContext("IsCustomEmoji", emojiId.HasValue)
            .Information("Discord reaction event recorded.");
    }
}
