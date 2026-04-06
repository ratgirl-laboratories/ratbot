using System.Diagnostics;
using Discord.Net;
using RatBot.Interactions.Common.Responses;

namespace RatBot.Interactions.Common.Discord;

/// <summary>
/// Provides shared helpers for guild-based Discord slash-command and modal modules.
/// </summary>
public abstract class SlashCommandBase : InteractionModuleBase<SocketInteractionContext>
{
    private const string DiagEventName = "interaction_diagnostics";
    private const string GuildOnlyMessage = "This command can only be used in a guild.";
    private const string UnexpectedErrorMessage = "An unexpected error occurred while handling that command.";

    private static InteractionResponse CreateResponse(string content, bool isEphemeral) =>
        isEphemeral
            ? InteractionResponse.Ephemeral(content)
            : InteractionResponse.Public(content);

    /// <summary>
    /// Replies ephemerally with plain text.
    /// </summary>
    protected Task ReplyAsync(string content) => RunAsync(
        InteractionResponse.Ephemeral(content),
        static response => Task.FromResult(response),
        defer: false);

    /// <summary>
    /// Replies publicly with plain text.
    /// </summary>
    protected Task ReplyPublicAsync(string content)
    {
        return RunAsync(InteractionResponse.Public(content), static response => Task.FromResult(response), defer: false);
    }

    /// <summary>
    /// Replies using an explicit interaction response.
    /// </summary>
    protected Task ReplyAsync(InteractionResponse response)
    {
        return RunAsync(response, static value => Task.FromResult(value), defer: false);
    }

    /// <summary>
    /// Executes a handler that returns plain text and replies ephemerally.
    /// </summary>
    /// <param name="handler">The handler to execute.</param>
    /// <param name="defer">Whether the interaction should be deferred before running the handler.</param>
    /// <returns>A task that completes when execution and response handling are finished.</returns>
    protected Task ReplyAsync(Func<Task<string>> handler, bool defer = false) =>
        RunAsync((Handler: handler, IsEphemeral: true), static async state => CreateResponse(await state.Handler(), state.IsEphemeral), defer);

    /// <summary>
    /// Executes a handler using typed command arguments and replies ephemerally.
    /// </summary>
    protected Task ReplyAsync<TArgs>(TArgs args, Func<TArgs, Task<string>> handler, bool defer = false) =>
        RunAsync(
            (Args: args, Handler: handler, IsEphemeral: true),
            static async state => CreateResponse(await state.Handler(state.Args), state.IsEphemeral),
            defer);

    /// <summary>
    /// Executes a handler that returns plain text and replies publicly.
    /// </summary>
    protected Task ReplyPublicAsync(Func<Task<string>> handler, bool defer = false) =>
        RunAsync((Handler: handler, IsEphemeral: false), static async state => CreateResponse(await state.Handler(), state.IsEphemeral), defer);

    /// <summary>
    /// Executes a handler using typed command arguments and replies publicly.
    /// </summary>
    protected Task ReplyPublicAsync<TArgs>(TArgs args, Func<TArgs, Task<string>> handler, bool defer = false) =>
        RunAsync(
            (Args: args, Handler: handler, IsEphemeral: false),
            static async state => CreateResponse(await state.Handler(state.Args), state.IsEphemeral),
            defer);

    /// <summary>
    /// Executes a handler that returns an explicit interaction response.
    /// </summary>
    protected Task ReplyAsync(Func<Task<InteractionResponse>> handler, bool defer = false) => RunAsync(handler, static value => value(), defer);

    /// <summary>
    /// Executes a handler using typed command arguments and an explicit interaction response.
    /// </summary>
    protected Task ReplyAsync<TArgs>(TArgs args, Func<TArgs, Task<InteractionResponse>> handler, bool defer = false) =>
        RunAsync(args, handler, defer);

    /// <summary>
    /// Defers the current interaction using an ephemeral acknowledgement.
    /// </summary>
    /// <returns><see langword="true"/> when the interaction was deferred or already acknowledged; otherwise, <see langword="false"/>.</returns>
    protected async Task<bool> TryDeferEphemeralAsync() => await TryDeferAsync(ephemeral: true);

    /// <summary>
    /// Defers the current interaction using a public acknowledgement.
    /// </summary>
    /// <returns><see langword="true"/> when the interaction was deferred or already acknowledged; otherwise, <see langword="false"/>.</returns>
    protected async Task<bool> TryDeferPublicAsync() => await TryDeferAsync(ephemeral: false);

    /// <summary>
    /// Sends an ephemeral message as either an initial response or a follow-up.
    /// </summary>
    /// <param name="text">The text to send.</param>
    protected async Task SendEphemeralAsync(string text) => await TrySendAsync(text, ephemeral: true);

    /// <summary>
    /// Creates a logger with standardized interaction and method context fields.
    /// </summary>
    /// <param name="methodContext">The logical method context name.</param>
    /// <returns>The enriched logger.</returns>
    protected ILogger CreateMethodLogger(string methodContext) =>
        InteractionLoggerContext.Create(Context, GetType().FullName, $"{GetType().Name}.{methodContext}");

    private async Task RunAsync<TState>(TState state, Func<TState, Task<InteractionResponse>> handler, bool defer)
    {
        if (Context.Guild is null)
        {
            await TrySendAsync(GuildOnlyMessage, ephemeral: true);
            return;
        }

        if (defer)
        {
            bool deferred = await TryDeferAsync(ephemeral: true);

            if (!deferred)
                return;
        }

        try
        {
            InteractionResponse response = await handler(state);
            await TrySendAsync(response);
        }
        catch (Exception ex)
        {
            CreateMethodLogger(nameof(RunAsync)).Error(ex, "Interaction handler execution failed.");
            await TrySendAsync(UnexpectedErrorMessage, ephemeral: true);
        }
    }

    private async Task<bool> TryDeferAsync(bool ephemeral)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ILogger logger = CreateInteractionDiagnosticsLogger("interaction_ack", nameof(TryDeferAsync));
        double interactionAgeMs = Math.Round(DateTimeOffset.UtcNow.Subtract(Context.Interaction.CreatedAt).TotalMilliseconds, 2);
        bool hasRespondedBefore = Context.Interaction.HasResponded;

        try
        {
            if (!Context.Interaction.HasResponded)
                await DeferAsync(ephemeral: ephemeral);

            logger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} has_responded_before={has_responded_before} has_responded_after={has_responded_after}",
                "defer",
                "success",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                hasRespondedBefore,
                Context.Interaction.HasResponded);

            return true;
        }
        catch (TimeoutException)
        {
            logger.Warning(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} has_responded_before={has_responded_before}",
                "defer",
                "timeout",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                hasRespondedBefore);

            return false;
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)40060)
        {
            logger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} discord_error_code={discord_error_code}",
                "defer",
                "already_acknowledged",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                (int)ex.DiscordCode);

            return true;
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)10062)
        {
            logger.Warning(
                ex,
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} discord_error_code={discord_error_code}",
                "defer",
                "unknown_interaction",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                (int)ex.DiscordCode);

            return false;
        }
    }

    private async Task TrySendAsync(InteractionResponse response)
    {
        await TrySendAsync(response.Content, response.IsEphemeral);
    }

    private async Task TrySendAsync(string content, bool ephemeral)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ILogger logger = CreateInteractionDiagnosticsLogger("interaction_send", nameof(TrySendAsync));
        double interactionAgeMs = Math.Round(DateTimeOffset.UtcNow.Subtract(Context.Interaction.CreatedAt).TotalMilliseconds, 2);
        bool hasRespondedBefore = Context.Interaction.HasResponded;

        try
        {
            string sendMode;

            if (Context.Interaction.HasResponded)
            {
                await FollowupAsync(content, ephemeral: ephemeral);
                sendMode = "followup";
            }
            else
            {
                await RespondAsync(content, ephemeral: ephemeral);
                sendMode = "respond";
            }

            logger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} send_ms={send_ms} interaction_age_ms={interaction_age_ms} send_mode={send_mode} ephemeral={ephemeral} has_responded_before={has_responded_before} has_responded_after={has_responded_after}",
                "send",
                "success",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                sendMode,
                ephemeral,
                hasRespondedBefore,
                Context.Interaction.HasResponded);
        }
        catch (TimeoutException)
        {
            logger.Warning(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} send_ms={send_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} has_responded_before={has_responded_before}",
                "send",
                "timeout",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                hasRespondedBefore);
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)10062)
        {
            logger.Warning(
                ex,
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} send_ms={send_ms} interaction_age_ms={interaction_age_ms} ephemeral={ephemeral} discord_error_code={discord_error_code}",
                "send",
                "unknown_interaction",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                interactionAgeMs,
                ephemeral,
                (int)ex.DiscordCode);
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)40060)
        {
            try
            {
                await FollowupAsync(content, ephemeral: ephemeral);

                logger.Information(
                    "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} send_ms={send_ms} interaction_age_ms={interaction_age_ms} send_mode={send_mode} ephemeral={ephemeral} discord_error_code={discord_error_code}",
                    "send",
                    "already_acknowledged_followup_success",
                    Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                    interactionAgeMs,
                    "followup",
                    ephemeral,
                    (int)ex.DiscordCode);
            }
            catch (Exception)
            {
                logger.Warning(
                    "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} send_ms={send_ms} interaction_age_ms={interaction_age_ms} send_mode={send_mode} ephemeral={ephemeral} discord_error_code={discord_error_code}",
                    "send",
                    "already_acknowledged_followup_failed",
                    Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                    interactionAgeMs,
                    "followup",
                    ephemeral,
                    (int)ex.DiscordCode);
            }
        }
    }

    private ILogger CreateInteractionDiagnosticsLogger(string diagComponent, string methodContext)
    {
        return CreateMethodLogger(methodContext).ForContext("diag_event", DiagEventName).ForContext("diag_component", diagComponent);
    }
}