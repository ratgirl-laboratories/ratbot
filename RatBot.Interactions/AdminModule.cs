using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;

namespace RatBot.Interactions;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule : SlashCommandBase
{
    internal const int DiscordMessageLimit = 2000;
    internal const int ModalMessageLimit = 4000;
    internal const string SayModalCustomId = "admin-say";
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

    internal static bool TryTakePendingChannelId(ulong guildId, ulong userId, out ulong channelId)
    {
        string pendingKey = GetPendingRequestKey(guildId, userId);
        bool found = PendingRequests.TryRemove(pendingKey, out PendingAdminSayRequest? pendingRequest);

        channelId = pendingRequest?.ChannelId ?? 0;
        return found;
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

    internal static IReadOnlyList<string> SplitIntoChunks(string message, int chunkSize)
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
