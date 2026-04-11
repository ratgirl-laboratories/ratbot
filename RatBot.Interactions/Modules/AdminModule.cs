using RatBot.Application.Features.AdminSay;

namespace RatBot.Interactions.Modules.Administration;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule(AdminSayWorkflowService workflowService) : SlashCommandBase
{
    private const string SayModalCustomIdPrefix = "admin-say";
    private const int ModalMessageLimit = 4000;

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

        AdminSaySession session = await workflowService.CreateSessionAsync(guild.Id, Context.User.Id, channel.Id);
        await RespondWithModalAsync<AdminSayModal>($"{SayModalCustomIdPrefix}:{session.SessionId}");
    }

    [ModalInteraction($"{SayModalCustomIdPrefix}:*", true)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SayModalAsync(string sessionId, AdminSayModal modal)
    {
        SocketGuild guild = Context.Guild!;
        AdminSaySession? session = await workflowService.ConsumeSessionAsync(sessionId, guild.Id, Context.User.Id);

        if (session is null)
        {
            await RespondAsync("No pending destination channel was found. Run `/admin say` again.", ephemeral: true);
            return;
        }

        ITextChannel? channel = guild.GetTextChannel(session.ChannelId);

        if (channel is null)
        {
            await RespondAsync("I couldn't find that channel. Run `/admin say` again.", ephemeral: true);
            return;
        }

        ChannelPermissions botPermissions = guild.CurrentUser.GetPermissions(channel);

        if (!botPermissions.ViewChannel || !botPermissions.SendMessages)
        {
            await RespondAsync($"I don't have permission to post in {channel.Mention}.", ephemeral: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(modal.Message))
        {
            await RespondAsync("Message cannot be empty.", ephemeral: true);
            return;
        }

        if (!await TryDeferEphemeralAsync())
            return;

        IReadOnlyList<string> messageChunks = DiscordMessageChunker.SplitForMessageLimit(modal.Message);

        foreach (string chunk in messageChunks)
            await channel.SendMessageAsync(chunk);

        string response = messageChunks.Count == 1
            ? $"Sent your message to {channel.Mention}."
            : $"Sent your message to {channel.Mention} in {messageChunks.Count} parts.";

        await SendEphemeralAsync(response);
    }

    [UsedImplicitly]
    public sealed record AdminSayModal : IModal
    {
        [InputLabel("Message")]
        [ModalTextInput(
            "message",
            TextInputStyle.Paragraph,
            "Write the message exactly as it should be posted.",
            maxLength: ModalMessageLimit)]
        public string Message { get; set; } = string.Empty;

        public string Title => "Send Message";
    }
}