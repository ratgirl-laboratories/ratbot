using Discord;
using Discord.Interactions;
using Discord.Net;

namespace RatBot.Interactions;

public abstract class SlashCommandBase : InteractionModuleBase<SocketInteractionContext>
{
    protected async Task<bool> TryDeferEphemeralAsync()
    {
        try
        {
            if (!Context.Interaction.HasResponded)
                await DeferAsync(ephemeral: true);

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

    protected async Task SendEphemeralAsync(string text)
    {
        try
        {
            if (Context.Interaction.HasResponded)
                await FollowupAsync(text, ephemeral: true);
            else
                await RespondAsync(text, ephemeral: true);
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
            await FollowupAsync(text, ephemeral: true);
        }
    }
}
