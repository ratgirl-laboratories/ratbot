namespace RatBot.Interactions.Modules;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule : SlashCommandBase
{
    private const string SendModalCustomIdPrefix = "admin-send";
    private const string MessageInputCustomId = "message";
    private const int ModalMessageLimit = 4000;

    [SlashCommand("send", "Send a multiline message as the bot to a specific channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendAsync(ITextChannel channel)
    {
        ErrorOr<Success> permissions = ValidateBotPermissions(channel);

        if (permissions.IsError)
        {
            await RespondEphemeralAsync(permissions.FirstError.Description);
            return;
        }

        await RespondWithModalAsync(BuildSendModal(channel.Id));
    }

    [ModalInteraction($"{SendModalCustomIdPrefix}:*:*", true)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendModalAsync(ulong _, ulong channelId)
    {
        SocketModal modal = (SocketModal)Context.Interaction;

        string message = modal.Data.Components
            .Single(x => x.CustomId == MessageInputCustomId)
            .Value;

        ErrorOr<string> result = await ProcessAdminSendAsync(channelId, message);

        if (result.IsError)
        {
            await RespondEphemeralAsync(result.FirstError.Description);
            return;
        }

        await RespondEphemeralAsync(result.Value);
    }

    private async Task<ErrorOr<string>> ProcessAdminSendAsync(ulong channelId, string message) =>
        await GetTextChannel(channelId)
            .Then(channel => ValidateBotPermissions(channel).Then(_ => channel))
            .ThenDoAsync(_ => DeferAsync())
            .Then(channel => DiscordUtils
                .SplitMessageIntoChunks(message)
                .Then(chunks => (Channel: channel, Chunks: chunks)))
            .ThenAsync(x => SendChunksAsync(x.Channel, x.Chunks));

    private Modal BuildSendModal(ulong channelId) =>
        new ModalBuilder()
            .WithTitle("Admin Send")
            .WithCustomId($"{SendModalCustomIdPrefix}:{Context.User.Id}:{channelId}")
            .AddTextInput(
                "Message",
                MessageInputCustomId,
                TextInputStyle.Paragraph,
                "Message to send as the bot.",
                required: true,
                minLength: 1,
                maxLength: ModalMessageLimit)
            .Build();

    private async static Task<string> SendChunksAsync(SocketTextChannel channel, string[] chunks)
    {
        foreach (string chunk in chunks)
            await channel.SendMessageAsync(chunk);

        return chunks.Length == 1
            ? $"Sent your message to {channel.Mention}."
            : $"Sent your message to {channel.Mention} in {chunks.Length} parts.";
    }

    private ErrorOr<Success> ValidateBotPermissions(ITextChannel channel)
    {
        ChannelPermissions permissions = Context.Guild.CurrentUser.GetPermissions(channel);

        if (!permissions.ViewChannel || !permissions.SendMessages)
            return AdminSendErrors.InsufficientPermissions;

        return Result.Success;
    }

    private ErrorOr<SocketTextChannel> GetTextChannel(ulong channelId) => Context.Guild.FetchTextChannel(channelId);

    private Task RespondEphemeralAsync(string message) =>
        Context.Interaction.HasResponded
            ? FollowupAsync(message, ephemeral: true)
            : RespondAsync(message, ephemeral: true);
}