using Discord;
using Discord.Interactions;

namespace RatBot.Interactions;

[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModalModule : SlashCommandBase
{
    [ModalInteraction(AdminModule.SayModalCustomId)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayModalAsync(AdminModule.AdminSayModal modal)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (!AdminModule.TryTakePendingChannelId(Context.Guild.Id, Context.User.Id, out ulong channelId))
        {
            await RespondAsync("No pending destination channel was found. Run `/admin say` again.", ephemeral: true);
            return;
        }

        ITextChannel? channel = Context.Guild.GetTextChannel(channelId);
        if (channel is null)
        {
            await RespondAsync(
                "I couldn't find that destination channel anymore. Run `/admin say` again.",
                ephemeral: true
            );
            return;
        }

        ChannelPermissions botPermissions = Context.Guild.CurrentUser.GetPermissions(channel);
        if (!botPermissions.ViewChannel || !botPermissions.SendMessages)
        {
            await RespondAsync($"I don't have permission to post in {channel.Mention}.", ephemeral: true);
            return;
        }

        string message = modal.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            await RespondAsync("Message cannot be empty.", ephemeral: true);
            return;
        }

        if (!await TryDeferEphemeralAsync())
            return;

        IReadOnlyList<string> messageChunks = AdminModule.SplitIntoChunks(message, AdminModule.DiscordMessageLimit);
        foreach (string chunk in messageChunks)
            await channel.SendMessageAsync(chunk);

        if (messageChunks.Count == 1)
        {
            await SendEphemeralAsync($"Sent your message to {channel.Mention}.");
            return;
        }

        await SendEphemeralAsync(
            $"Sent your message to {channel.Mention} in {messageChunks.Count} parts (Discord's limit is {AdminModule.DiscordMessageLimit} characters per message)."
        );
    }
}
