using System.Collections.Concurrent;
using System.Reflection;
using Discord.Commands;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Discord;

public sealed class DiscordBotService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly CommandService _commandService;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly VirtueModule _virtueModule;
    private readonly ConcurrentDictionary<ulong, string> _prefixes = new ConcurrentDictionary<ulong, string>();

    public DiscordBotService(
        DiscordSocketClient discordClient,
        CommandService commandService,
        InteractionService interactionService,
        IServiceProvider services,
        IConfiguration config,
        VirtueModule virtueModule
    )
    {
        _discordClient = discordClient;
        _commandService = commandService;
        _interactionService = interactionService;
        _services = services;
        _config = config;
        _virtueModule = virtueModule;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _discordClient.Log += msg =>
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        };

        Assembly prefixCommandsAssembly = Assembly.Load("RatBot.Commands");
        Assembly slashCommandsAssembly = Assembly.Load("RatBot.Interactions");
        Assembly mainAssembly = Assembly.GetExecutingAssembly();

        await _commandService.AddModulesAsync(prefixCommandsAssembly, _services);
        Console.WriteLine($"[DiscordBot] Registered {_commandService.Modules.Count()} command modules.");

        await _interactionService.AddModulesAsync(mainAssembly, _services);
        await _interactionService.AddModulesAsync(slashCommandsAssembly, _services);
        Console.WriteLine($"[DiscordBot] Registered {_interactionService.Modules.Count} interaction modules.");

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
                    Console.WriteLine(
                        $"[DiscordBot] Slash commands registered to guild {devGuildId} (development mode)."
                    );

                    try
                    {
                        await ClearGlobalApplicationCommandsAsync();
                        Console.WriteLine(
                            "[DiscordBot] Cleared global application commands to prevent duplicates while using per‑guild registration."
                        );
                    }
                    catch (Exception clearEx)
                    {
                        Console.WriteLine($"[DiscordBot] Failed to clear global application commands: {clearEx}");
                    }
                }
                else
                {
                    await _interactionService.RegisterCommandsGloballyAsync();
                    Console.WriteLine("[DiscordBot] Slash commands registered globally.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DiscordBot] Failed to register slash commands: {ex}");
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
        _virtueModule.RegisterHandlers();

        // Forward InteractionService logs to console to aid troubleshooting
        _interactionService.Log += msg =>
        {
            Console.WriteLine($"[Interactions] {msg}");
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
        if (interaction.Type != InteractionType.ApplicationCommand)
            return;

        try
        {
            IInteractionContext context = new SocketInteractionContext(_discordClient, interaction);
            await _interactionService.ExecuteCommandAsync(context, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DiscordBot] Interaction execution failed: {ex}");

            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("An error occurred executing that command.");
                else
                    await interaction.FollowupAsync("An error occurred executing that command.");
            }
            catch (Exception followupEx)
            {
                Console.WriteLine($"[DiscordBot] Failed to send error followup: {followupEx}");
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
                Console.WriteLine(
                    $"[DiscordBot] Failed to delete global command '{cmd.Name}' ({cmd.Id}): {ex.Message}"
                );
            }
    }
}
