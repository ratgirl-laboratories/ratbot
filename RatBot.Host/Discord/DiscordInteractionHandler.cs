using System.Diagnostics;
using System.Reflection;
using Discord.Interactions;
using Discord.Net;
using Microsoft.Extensions.Options;
using RatBot.Host.Configuration;
using RatBot.Interactions.Modules;
using IResult = Discord.Interactions.IResult;

namespace RatBot.Host.Discord;

public sealed class DiscordInteractionHandler(
    DiscordSocketClient discordClient,
    InteractionService interactionService,
    IServiceProvider services,
    IOptions<DiscordOptions> options,
    IConfiguration configuration,
    ILogger logger)
{
    private const string DiagEventName = "interaction_diagnostics";
    private readonly ILogger _logger = logger.ForContext<DiscordInteractionHandler>();

    private readonly DiscordOptions _options = options.Value;

    private readonly string _serviceInstanceId =
        configuration["OTEL:Resource:ServiceInstanceId"] ?? Environment.MachineName;

    private static string GetInteractionName(SocketInteraction interaction) =>
        interaction switch
        {
            SocketSlashCommand slashCommand => GetSlashCommandName(slashCommand),
            SocketUserCommand userCommand => userCommand.Data.Name,
            SocketMessageCommand messageCommand => messageCommand.Data.Name,
            SocketMessageComponent component => component.Data.CustomId,
            SocketModal modal => modal.Data.CustomId,
            _ => interaction.Type.ToString()
        };

    private static string GetSlashCommandName(SocketSlashCommand command)
    {
        List<string> parts = [command.Data.Name];
        IReadOnlyCollection<SocketSlashCommandDataOption> options = command.Data.Options;

        while (true)
        {
            SocketSlashCommandDataOption? subCommandOption = options.FirstOrDefault(option =>
                option.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup);

            if (subCommandOption is null)
                return string.Join(" ", parts);

            parts.Add(subCommandOption.Name);
            options = subCommandOption.Options;
        }
    }

    private static IEnumerable<SocketSlashCommandDataOption> EnumerateSlashOptions(
        IReadOnlyCollection<SocketSlashCommandDataOption> options)
    {
        foreach (SocketSlashCommandDataOption option in options)
        {
            yield return option;

            foreach (SocketSlashCommandDataOption nested in EnumerateSlashOptions(option.Options))
                yield return nested;
        }
    }

    private static CommandUsageDetails GetCommandUsageDetails(SocketInteraction interaction) =>
        interaction switch
        {
            SocketSlashCommand slashCommand => GetSlashUsageDetails(slashCommand),
            SocketUserCommand userCommand => GetUserContextUsageDetails(userCommand),
            SocketMessageCommand messageCommand => GetMessageContextUsageDetails(messageCommand),
            _ => new CommandUsageDetails(GetInteractionName(interaction), null, null, null)
        };

    private static CommandUsageDetails GetSlashUsageDetails(SocketSlashCommand slashCommand)
    {
        string commandName = GetSlashCommandName(slashCommand);

        foreach (SocketSlashCommandDataOption option in EnumerateSlashOptions(slashCommand.Data.Options))
        {
            if (option.Type is not (ApplicationCommandOptionType.User or ApplicationCommandOptionType.Mentionable))
                continue;

            switch (option.Value)
            {
                case SocketGuildUser guildUser:
                    return new CommandUsageDetails(
                        commandName,
                        guildUser.Id,
                        guildUser.Username,
                        $"slash_option:{option.Name}");
                case SocketUser socketUser:
                    return new CommandUsageDetails(
                        commandName,
                        socketUser.Id,
                        socketUser.Username,
                        $"slash_option:{option.Name}");
                case IUser user:
                    return new CommandUsageDetails(commandName, user.Id, user.Username, $"slash_option:{option.Name}");
                case ulong userId:
                    return new CommandUsageDetails(commandName, userId, null, $"slash_option:{option.Name}");
            }
        }

        return new CommandUsageDetails(commandName, null, null, null);
    }

    private static CommandUsageDetails GetUserContextUsageDetails(SocketUserCommand userCommand)
    {
        IUser? invokee = userCommand.Data.Member ?? ((IUserCommandInteractionData)userCommand.Data).User;
        return new CommandUsageDetails(userCommand.Data.Name, invokee?.Id, invokee?.Username, "user_context_target");
    }

    private static CommandUsageDetails GetMessageContextUsageDetails(SocketMessageCommand messageCommand)
    {
        IUser? invokee = messageCommand.Data.Message.Author;

        return new CommandUsageDetails(
            messageCommand.Data.Name,
            invokee?.Id,
            invokee?.Username,
            "message_context_author");
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        Assembly interactionsAssembly = typeof(HelloModule).Assembly;
        await interactionService.AddModulesAsync(interactionsAssembly, services);

        _logger.Information(
            "Registered {InteractionModuleCount} interaction modules.",
            interactionService.Modules.Count);
        LogRegisteredInteractionCommands();

        discordClient.InteractionCreated += HandleInteractionAsync;
        discordClient.Ready += RegisterCommandsAsync;
    }

    public void Unsubscribe()
    {
        discordClient.InteractionCreated -= HandleInteractionAsync;
        discordClient.Ready -= RegisterCommandsAsync;
    }

    private async Task RegisterCommandsAsync()
    {
        try
        {
            await interactionService.RegisterCommandsToGuildAsync(_options.GuildId);
            _logger.Information("Slash commands registered to guild {GuildId}.", _options.GuildId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to register slash commands.");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (interaction.Type is not (InteractionType.ApplicationCommand or InteractionType.ModalSubmit
            or InteractionType.MessageComponent))
            return;

        Stopwatch totalStopwatch = Stopwatch.StartNew();

        ILogger interactionLogger = CreateInteractionDiagnosticsLogger(interaction)
            .ForContext("diag_component", "interaction_dispatch");

        try
        {
            IInteractionContext context = new SocketInteractionContext(discordClient, interaction);

            interactionLogger = interactionLogger.ForContext("user_id", context.User.Id)
                .ForContext("guild_id", context.Guild?.Id)
                .ForContext("channel_id", context.Channel?.Id);

            if (interaction.Type == InteractionType.ApplicationCommand)
                LogCommandUsage(interactionLogger, context, interaction);

            IResult result = await interactionService.ExecuteCommandAsync(context, services);

            if (result.IsSuccess)
            {
                interactionLogger.Information(
                    "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms} has_responded={has_responded}",
                    "execute",
                    "success",
                    Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2),
                    interaction.HasResponded);

                return;
            }

            interactionLogger.Warning(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} command_error={command_error} command_reason={command_reason}",
                "execute",
                "failed",
                result.Error?.ToString() ?? "None",
                result.ErrorReason ?? string.Empty);

            string reason = string.IsNullOrWhiteSpace(result.ErrorReason)
                ? "Command execution failed."
                : result.ErrorReason;

            if (!interaction.HasResponded)
                await interaction.RespondAsync($"Command failed: {reason}", ephemeral: true);
            else
                await interaction.FollowupAsync($"Command failed: {reason}", ephemeral: true);
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)10062)
        {
            interactionLogger.Warning(ex, "Unknown interaction received by dispatch pipeline.");
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)40060)
        {
            interactionLogger.Information(ex, "Interaction was already acknowledged.");
        }
        catch (Exception ex)
        {
            interactionLogger.Error(ex, "Unhandled exception executing interaction.");

            try
            {
                if (!interaction.HasResponded)
                    await interaction.RespondAsync("An error occurred executing that command.", ephemeral: true);
                else
                    await interaction.FollowupAsync("An error occurred executing that command.", ephemeral: true);
            }
            catch (Exception followupEx)
            {
                interactionLogger.Warning(followupEx, "Failed to send interaction error response.");
            }
        }
    }

    private void LogCommandUsage(ILogger interactionLogger, IInteractionContext context, SocketInteraction interaction)
    {
        CommandUsageDetails usage = GetCommandUsageDetails(interaction);

        interactionLogger.ForContext("method_context", $"{nameof(DiscordInteractionHandler)}.{nameof(LogCommandUsage)}")
            .ForContext("command_name", usage.CommandName)
            .ForContext("invoker_user_id", context.User.Id)
            .ForContext("invokee_user_id", usage.InvokeeUserId)
            .ForContext("invokee_username", usage.InvokeeUsername)
            .ForContext("invokee_source", usage.InvokeeSource)
            .Information("Command Invoked");
    }

    private void LogRegisteredInteractionCommands()
    {
        _logger.Information(
            "Registered interaction commands. SlashCommandCount={SlashCommandCount} ContextCommandCount={ContextCommandCount} ComponentCommandCount={ComponentCommandCount} ModalCommandCount={ModalCommandCount}",
            interactionService.SlashCommands.Count,
            interactionService.ContextCommands.Count,
            interactionService.ComponentCommands.Count,
            interactionService.ModalCommands.Count);

        foreach (ModuleInfo module in EnumerateModules(interactionService.Modules))
        {
            _logger.Information(
                "Registered interaction module. Module={InteractionModule} SlashGroup={SlashGroup} SlashCommandCount={SlashCommandCount} ContextCommandCount={ContextCommandCount} ComponentCommandCount={ComponentCommandCount} ModalCommandCount={ModalCommandCount}",
                module.Name,
                module.SlashGroupName,
                module.SlashCommands.Count,
                module.ContextCommands.Count,
                module.ComponentCommands.Count,
                module.ModalCommands.Count);

            foreach (SlashCommandInfo command in module.SlashCommands)
                _logger.Information(
                    "Registered interaction command. Type={InteractionCommandType} Module={InteractionModule} SlashGroup={SlashGroup} Name={InteractionCommandName} Method={InteractionCommandMethod}",
                    "Slash",
                    module.Name,
                    module.SlashGroupName,
                    command.Name,
                    command.MethodName);

            foreach (ContextCommandInfo command in module.ContextCommands)
                _logger.Information(
                    "Registered interaction command. Type={InteractionCommandType} Module={InteractionModule} Name={InteractionCommandName} Method={InteractionCommandMethod}",
                    "Context",
                    module.Name,
                    command.Name,
                    command.MethodName);

            foreach (ComponentCommandInfo command in module.ComponentCommands)
                _logger.Information(
                    "Registered interaction command. Type={InteractionCommandType} Module={InteractionModule} Name={InteractionCommandName} Method={InteractionCommandMethod} SupportsWildCards={SupportsWildCards}",
                    "Component",
                    module.Name,
                    command.Name,
                    command.MethodName,
                    command.SupportsWildCards);

            foreach (ModalCommandInfo command in module.ModalCommands)
                _logger.Information(
                    "Registered interaction command. Type={InteractionCommandType} Module={InteractionModule} Name={InteractionCommandName} Method={InteractionCommandMethod} SupportsWildCards={SupportsWildCards} ModalType={ModalType}",
                    "Modal",
                    module.Name,
                    command.Name,
                    command.MethodName,
                    command.SupportsWildCards,
                    command.Modal.Type.FullName);
        }
    }

    private static IEnumerable<ModuleInfo> EnumerateModules(IEnumerable<ModuleInfo> modules)
    {
        foreach (ModuleInfo module in modules)
        {
            yield return module;

            foreach (ModuleInfo subModule in EnumerateModules(module.SubModules))
                yield return subModule;
        }
    }

    private ILogger CreateInteractionDiagnosticsLogger(SocketInteraction interaction) =>
        _logger.ForContext("diag_event", DiagEventName)
            .ForContext("service_instance_id", _serviceInstanceId)
            .ForContext("process_id", Environment.ProcessId)
            .ForContext("interaction_id", interaction.Id)
            .ForContext("interaction_type", interaction.Type.ToString())
            .ForContext("interaction_name", GetInteractionName(interaction))
            .ForContext("interaction_created_at_utc", interaction.CreatedAt.UtcDateTime.ToString("O"));

    private readonly record struct CommandUsageDetails(
        string CommandName,
        ulong? InvokeeUserId,
        string? InvokeeUsername,
        string? InvokeeSource);
}
