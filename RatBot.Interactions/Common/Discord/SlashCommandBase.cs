using Discord;
using Discord.Interactions;
using Discord.Net;
using RatBot.Interactions.Common.Responses;
using Serilog;

namespace RatBot.Interactions.Common.Discord;

/// <summary>
/// Provides shared helpers for guild-based Discord slash-command and modal modules.
/// </summary>
public abstract class SlashCommandBase : InteractionModuleBase<SocketInteractionContext>
{
    private const string GuildOnlyMessage = "This command can only be used in a guild.";
    private const string UnexpectedErrorMessage = "An unexpected error occurred while handling that command.";

    private static InteractionResponse CreateResponse(string content, bool isEphemeral)
    {
        return isEphemeral ? InteractionResponse.Ephemeral(content) : InteractionResponse.Public(content);
    }

    /// <summary>
    /// Replies ephemerally with plain text.
    /// </summary>
    protected Task ReplyAsync(string content)
    {
        return RunAsync(InteractionResponse.Ephemeral(content), static response => Task.FromResult(response), defer: false);
    }

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
            defer
        );

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
            defer
        );

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
    /// Sends an ephemeral message as either an initial response or a follow-up.
    /// </summary>
    /// <param name="text">The text to send.</param>
    protected async Task SendEphemeralAsync(string text) => await TrySendAsync(text, ephemeral: true);

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
            Log.ForContext("SourceContext", GetType().FullName).Error(ex, "Interaction handler execution failed.");
            await TrySendAsync(UnexpectedErrorMessage, ephemeral: true);
        }
    }

    private async Task<bool> TryDeferAsync(bool ephemeral)
    {
        try
        {
            if (!Context.Interaction.HasResponded)
                await DeferAsync(ephemeral: ephemeral);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)40060)
        {
            return true;
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)10062)
        {
            return false;
        }
    }

    private async Task TrySendAsync(InteractionResponse response)
    {
        await TrySendAsync(response.Content, response.IsEphemeral);
    }

    private async Task TrySendAsync(string content, bool ephemeral)
    {
        try
        {
            if (Context.Interaction.HasResponded)
                await FollowupAsync(content, ephemeral: ephemeral);
            else
                await RespondAsync(content, ephemeral: ephemeral);
        }
        catch (TimeoutException)
        {
            // Interaction token already expired; cannot send a response.
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)10062)
        {
            // Interaction token already expired; cannot send a response.
        }
        catch (HttpException ex) when (ex.DiscordCode == (DiscordErrorCode)40060)
        {
            try
            {
                await FollowupAsync(content, ephemeral: ephemeral);
            }
            catch (Exception)
            {
                // The interaction has already been acknowledged and the follow-up also failed.
            }
        }
    }
}
