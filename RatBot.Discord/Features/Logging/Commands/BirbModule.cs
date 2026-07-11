using RatBot.Discord.Features.Logging.Gateway;

namespace RatBot.Discord.Features.Logging.Commands;

[DefaultMemberPermissions(GuildPermission.ManageRoles)]
public sealed class BirbModule(SerilogBackgroundWorker worker) : InteractionModuleBase<IInteractionContext>
{
    [SlashCommand("birb", "Summon the robin to a channel.")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task BirbAsync(
        [Summary("channel", "Channel to summon the robin to.")] [ChannelTypes(ChannelType.Text, ChannelType.News)] ITextChannel? channel = null
    )
    {
        channel ??= Context.Channel as ITextChannel;

        if (channel is null)
        {
            await RespondAsync("The robin cannot visit this channel.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await RespondAsync("Summoning the birb ...", ephemeral: true).ConfigureAwait(false);

        bool sent = await worker.PostOnceAsync(channel, CancellationToken.None).ConfigureAwait(false);
        await ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = sent ? "Birb deployed." : "Birb failed to appear.";
            })
            .ConfigureAwait(false);
    }
}
