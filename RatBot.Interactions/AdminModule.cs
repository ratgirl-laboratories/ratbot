using Discord;
using Discord.Interactions;

namespace RatBot.Interactions;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule : SlashCommandBase
{
    private const int DiscordMessageLimit = 2000;

    [SlashCommand("say", "Send a message as the bot to a specific channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayAsync(ITextChannel channel, string message)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            await RespondAsync("Message cannot be empty.", ephemeral: true);
            return;
        }

        ChannelPermissions botPermissions = Context.Guild.CurrentUser.GetPermissions(channel);
        if (!botPermissions.ViewChannel || !botPermissions.SendMessages)
        {
            await RespondAsync($"I don't have permission to post in {channel.Mention}.", ephemeral: true);
            return;
        }

        if (!await TryDeferEphemeralAsync())
            return;

        IReadOnlyList<string> messageChunks = SplitIntoChunks(message, DiscordMessageLimit);
        foreach (string chunk in messageChunks)
            await channel.SendMessageAsync(chunk);

        if (messageChunks.Count == 1)
        {
            await SendEphemeralAsync($"Sent your message to {channel.Mention}.");
            return;
        }

        await SendEphemeralAsync(
            $"Sent your message to {channel.Mention} in {messageChunks.Count} parts (Discord's limit is {DiscordMessageLimit} characters per message)."
        );
    }

    private static IReadOnlyList<string> SplitIntoChunks(string message, int chunkSize)
    {
        List<string> chunks = [];
        int index = 0;

        while (index < message.Length)
        {
            int remainingLength = message.Length - index;
            if (remainingLength <= chunkSize)
            {
                chunks.Add(message[index..]);
                break;
            }

            string window = message.Substring(index, chunkSize);
            int splitAt = window.LastIndexOf('\n');
            int chunkLength = splitAt > 0 ? splitAt + 1 : chunkSize;

            chunks.Add(message.Substring(index, chunkLength));
            index += chunkLength;
        }

        return chunks;
    }
}
