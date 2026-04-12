using RatBot.Domain.Extensions;

namespace RatBot.Interactions.Modules.Admin;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule : SlashCommandBase
{
    private const string SendModalCustomIdPrefix = "admin-send";

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

        string customId = $"{SendModalCustomIdPrefix}:{Context.User.Id}:{channel.Id}";

        await Context.Interaction.RespondWithModalAsync<AdminSendModal>(customId);
    }

    [ModalInteraction($"{SendModalCustomIdPrefix}:*:*", true)]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendModalAsync(ulong invokerUserId, ulong channelId, AdminSendModal modal)
    {
        if (Context.User.Id != invokerUserId)
        {
            await RespondEphemeralAsync("Only the user who opened this modal can submit it.");
            return;
        }

        ErrorOr<string> result = await ProcessAdminSendAsync(channelId, modal.Message);

        if (result.IsError)
        {
            await RespondEphemeralAsync(result.FirstError.Description);
            return;
        }

        await RespondEphemeralAsync(result.Value);
    }

    private async Task<ErrorOr<string>> ProcessAdminSendAsync(ulong channelId, string message) =>
        await GetTextChannel(channelId)
            .Ensure(ValidateBotPermissions)
            .ThenDoAsync(_ => DeferAsync())
            .Then(channel => DiscordUtils
                .SplitMessageIntoChunks(message)
                .Then(chunks => (Channel: channel, Chunks: chunks)))
            .ThenAsync(x => SendChunksAsync(x.Channel, x.Chunks));

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

    private ErrorOr<SocketTextChannel> GetTextChannel(ulong channelId) =>
        Context.Guild.FetchTextChannel(channelId);

    private Task RespondEphemeralAsync(string message) =>
        Context.Interaction.HasResponded
            ? FollowupAsync(message, ephemeral: true)
            : RespondAsync(message, ephemeral: true);
}