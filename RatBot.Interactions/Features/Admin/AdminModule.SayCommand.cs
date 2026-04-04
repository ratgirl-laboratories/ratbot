using System.Collections.Concurrent;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using JetBrains.Annotations;
using RatBot.Interactions.Common.Discord;

namespace RatBot.Interactions.Features.Admin;

[UsedImplicitly]
public sealed partial class AdminModule
{
    private const string SayModalCustomId = "admin-say";
    private const int ModalMessageLimit = 4000;

    private static readonly TimeSpan PendingRequestTtl = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, PendingAdminSayRequest> PendingRequests =
        new ConcurrentDictionary<string, PendingAdminSayRequest>();

    private static bool TryTakePendingChannelId(ulong guildId, ulong userId, out ulong channelId)
    {
        string pendingKey = $"{guildId}:{userId}";
        bool found = PendingRequests.TryRemove(pendingKey, out PendingAdminSayRequest? pendingRequest);

        channelId = pendingRequest?.ChannelId ?? 0;
        return found;
    }

    private static string GetPendingRequestKey(ulong guildId, ulong userId) => $"{guildId}:{userId}";

    private static void PurgeExpiredPendingRequests()
    {
        DateTimeOffset threshold = DateTimeOffset.UtcNow.Subtract(PendingRequestTtl);

        foreach ((string key, PendingAdminSayRequest pendingRequest) in PendingRequests)
            if (pendingRequest.CreatedAt < threshold)
                PendingRequests.TryRemove(key, out _);
    }

    /// <summary>
    /// Opens a modal to send a multi-line bot-authored message to a target channel.
    /// </summary>
    /// <param name="channel">The destination channel.</param>
    [SlashCommand("say", "Send a multiline message as the bot to a specific channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayAsync(ITextChannel channel)
    {
        SocketGuild guild = Context.Guild!;
        ChannelPermissions botPermissions = guild.CurrentUser.GetPermissions(channel);
        if (!botPermissions.ViewChannel || !botPermissions.SendMessages)
        {
            await RespondAsync($"I don't have permission to post in {channel.Mention}.", ephemeral: true);
            return;
        }

        PurgeExpiredPendingRequests();
        PendingRequests[GetPendingRequestKey(guild.Id, Context.User.Id)] = new PendingAdminSayRequest(channel.Id, DateTimeOffset.UtcNow);

        await RespondWithModalAsync<AdminSayModal>(SayModalCustomId);
    }

    /// <summary>
    /// Handles submission of the admin say modal and posts the message to the queued destination channel.
    /// </summary>
    /// <param name="modal">The modal payload.</param>
    [ModalInteraction(SayModalCustomId, ignoreGroupNames: true)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayModalAsync(AdminSayModal modal)
    {
        SocketGuild guild = Context.Guild!;

        if (!TryTakePendingChannelId(guild.Id, Context.User.Id, out ulong channelId))
        {
            await RespondAsync("No pending destination channel was found. Run `/admin say` again.", ephemeral: true);
            return;
        }

        ITextChannel? channel = guild.GetTextChannel(channelId);
        if (channel is null)
        {
            await RespondAsync("I couldn't find that destination channel anymore. Run `/admin say` again.", ephemeral: true);

            return;
        }

        ChannelPermissions botPermissions = guild.CurrentUser.GetPermissions(channel);
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

        IReadOnlyList<string> messageChunks = DiscordMessageChunker.SplitForMessageLimit(message);
        foreach (string chunk in messageChunks)
            await channel.SendMessageAsync(chunk);

        if (messageChunks.Count == 1)
        {
            await SendEphemeralAsync($"Sent your message to {channel.Mention}.");
            return;
        }

        await SendEphemeralAsync(
            $"Sent your message to {channel.Mention} in {messageChunks.Count} parts (Discord's limit is {DiscordMessageChunker.DiscordMessageCharacterLimit} characters per message)."
        );
    }

    /// <summary>
    /// Modal payload for the admin say command.
    /// </summary>
    [UsedImplicitly]
    public sealed class AdminSayModal : IModal
    {
        /// <summary>
        /// Gets the modal title displayed to the user.
        /// </summary>
        public string Title => "Send Message";

        /// <summary>
        /// Gets or sets the message body entered by the user.
        /// </summary>
        [InputLabel("Message")]
        [ModalTextInput(
            "message",
            TextInputStyle.Paragraph,
            placeholder: "Write the message exactly as it should be posted.",
            maxLength: ModalMessageLimit
        )]
        public string Message { get; set; } = string.Empty;
    }

    private sealed record PendingAdminSayRequest(ulong ChannelId, DateTimeOffset CreatedAt);
}
