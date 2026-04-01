using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;

namespace RatBot.Interactions;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule : SlashCommandBase
{
    private const int DiscordMessageLimit = 2000;
    private const int ModalMessageLimit = 4000;
    private const string SayModalCustomId = "admin-say";
    private static readonly TimeSpan PendingRequestTtl = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, PendingAdminSayRequest> PendingRequests = new();

    [SlashCommand("say", "Send a multiline message as the bot to a specific channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayAsync(ITextChannel channel)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        ChannelPermissions botPermissions = Context.Guild.CurrentUser.GetPermissions(channel);
        if (!botPermissions.ViewChannel || !botPermissions.SendMessages)
        {
            await RespondAsync($"I don't have permission to post in {channel.Mention}.", ephemeral: true);
            return;
        }

        PurgeExpiredPendingRequests();
        PendingRequests[GetPendingRequestKey(Context.Guild.Id, Context.User.Id)] = new PendingAdminSayRequest(
            channel.Id,
            DateTimeOffset.UtcNow
        );

        await RespondWithModalAsync<AdminSayModal>(SayModalCustomId);
    }

    [ModalInteraction(SayModalCustomId)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayModalAsync(AdminSayModal modal)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        string pendingKey = GetPendingRequestKey(Context.Guild.Id, Context.User.Id);
        if (!PendingRequests.TryRemove(pendingKey, out PendingAdminSayRequest? pendingRequest))
        {
            await RespondAsync("No pending destination channel was found. Run `/admin say` again.", ephemeral: true);
            return;
        }

        ITextChannel? channel = Context.Guild.GetTextChannel(pendingRequest.ChannelId);
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

    private static string GetPendingRequestKey(ulong guildId, ulong userId)
    {
        return $"{guildId}:{userId}";
    }

    private static void PurgeExpiredPendingRequests()
    {
        DateTimeOffset threshold = DateTimeOffset.UtcNow.Subtract(PendingRequestTtl);

        foreach ((string key, PendingAdminSayRequest pendingRequest) in PendingRequests)
            if (pendingRequest.CreatedAt < threshold)
                PendingRequests.TryRemove(key, out _);
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

    private sealed record PendingAdminSayRequest(ulong ChannelId, DateTimeOffset CreatedAt);

    public sealed class AdminSayModal : IModal
    {
        public string Title => "Send Message";

        [InputLabel("Message")]
        [ModalTextInput(
            "message",
            TextInputStyle.Paragraph,
            placeholder: "Write the message exactly as it should be posted.",
            maxLength: ModalMessageLimit
        )]
        public string Message { get; set; } = string.Empty;
    }
}
