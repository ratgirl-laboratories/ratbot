using System.Collections.Concurrent;
using System.Reflection;
using Discord.Commands;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;
using Serilog.Events;
using IResult = Discord.Interactions.IResult;

namespace RatBot.Discord;

public sealed class DiscordBotService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly CommandService _commandService;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<ulong, string> _prefixes = new ConcurrentDictionary<ulong, string>();

    public DiscordBotService(
        DiscordSocketClient discordClient,
        CommandService commandService,
        InteractionService interactionService,
        IServiceProvider services,
        IConfiguration config,
        ILogger logger
    )
    {
        _discordClient = discordClient;
        _commandService = commandService;
        _interactionService = interactionService;
        _services = services;
        _config = config;
        _logger = logger.ForContext<DiscordBotService>();
    }

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

        Assembly prefixCommandsAssembly = Assembly.Load("RatBot.Commands");
        Assembly slashCommandsAssembly = Assembly.Load("RatBot.Interactions");
        Assembly mainAssembly = Assembly.GetExecutingAssembly();

        await _commandService.AddModulesAsync(prefixCommandsAssembly, _services);
        _logger.Information("Registered {PrefixModuleCount} prefix command modules.", _commandService.Modules.Count());

        await _interactionService.AddModulesAsync(mainAssembly, _services);
        await _interactionService.AddModulesAsync(slashCommandsAssembly, _services);
        _logger.Information("Registered {InteractionModuleCount} interaction modules.", _interactionService.Modules.Count);

        _discordClient.Ready += async () =>
        {
            try
            {
                // Prefer fast per-guild registration in development if a Guild Id is configured; fall back to global
                string? devGuildIdStr = _config["Discord:DevelopmentGuildId"] ?? _config["Discord:GuildId"];
                if (!string.IsNullOrWhiteSpace(devGuildIdStr) && ulong.TryParse(devGuildIdStr, out ulong devGuildId))
                {
                    // Overwrite guild commands to match current modules (deleteMissing=true)
                    await _interactionService.RegisterCommandsToGuildAsync(devGuildId, deleteMissing: true);
                    _logger.Information("Slash commands registered to guild {GuildId} (development mode).", devGuildId);

                    try
                    {
                        await ClearGlobalApplicationCommandsAsync();
                        _logger.Information(
                            "Cleared global application commands to prevent duplicates while using per-guild registration."
                        );
                    }
                    catch (Exception clearEx)
                    {
                        _logger.Warning(clearEx, "Failed to clear global application commands.");
                    }
                }
                else
                {
                    await _interactionService.RegisterCommandsGloballyAsync();
                    _logger.Information("Slash commands registered globally.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to register slash commands.");
            }

            try
            {
                foreach (SocketGuild guild in _discordClient.Guilds)
                    await GetOrLoadPrefixAsync(guild.Id);
            }
            catch
            {
                // Ignore preload errors
            }
        };

        _discordClient.MessageReceived += ProcessMessageAsync;
        _discordClient.InteractionCreated += HandleInteractionAsync;

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
            IResult result = await _interactionService.ExecuteCommandAsync(
                context,
                _services
            );

            if (result.IsSuccess)
                return;

            _logger.Warning(
                "Interaction command failed. Error={Error} Reason={Reason}",
                result.Error,
                result.ErrorReason
            );

            string reason = string.IsNullOrWhiteSpace(result.ErrorReason)
                ? "Command execution failed."
                : result.ErrorReason;

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

    private async Task ProcessMessageAsync(SocketMessage rawMessage)
    {
        // Ignore system messages and messages from bots
        if (rawMessage is not SocketUserMessage message)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Channel is not SocketGuildChannel guildChannel)
            return;

        // Get the current guild prefix; load once per guild and cache for runtime
        string prefix = await GetOrLoadPrefixAsync(guildChannel.Guild.Id);

        int argPos = 0;
        if (!message.HasStringPrefix(prefix, ref argPos))
            return;

        SocketCommandContext context = new SocketCommandContext(_discordClient, message);
        await _commandService.ExecuteAsync(context, argPos, _services);
    }

    private async Task<string> GetOrLoadPrefixAsync(ulong guildId)
    {
        if (_prefixes.TryGetValue(guildId, out string? existing))
            return existing;

        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        GuildConfigService guildConfigService = scope.ServiceProvider.GetRequiredService<GuildConfigService>();
        GuildConfig guildConfig = await guildConfigService.GetOrCreateAsync(guildId);

        string prefix = string.IsNullOrWhiteSpace(guildConfig.Prefix) ? "?" : guildConfig.Prefix;
        _prefixes[guildId] = prefix;

        return prefix;
    }

    private async Task ClearGlobalApplicationCommandsAsync()
    {
        // Enumerate and delete existing global commands
        IReadOnlyCollection<RestGlobalCommand> existing = await _discordClient.Rest.GetGlobalApplicationCommands();

        if (existing.Count == 0)
            return;

        foreach (RestGlobalCommand cmd in existing)
            try
            {
                await cmd.DeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to delete global command {CommandName} ({CommandId}).", cmd.Name, cmd.Id);
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
}
