using RatBot.Application.Features.AdminSend;

namespace RatBot.Interactions.Modules.Admin;

[Group("admin", "Administrative commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdminModule(AdminSendService adminSendService) : InteractionModuleBase<IInteractionContext>
{
    private const string SendModalCustomIdPrefix = "admin-send";

    [SlashCommand("send", "Send a multiline message as the bot to a specific channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendAsync(ITextChannel channel)
    {
        IGuildUser currentUser = await Context.Guild.GetCurrentUserAsync();
        ChannelPermissions permissions = currentUser.GetPermissions(channel);

        if (!permissions.ViewChannel || !permissions.SendMessages)
        {
            await RespondEphemeralAsync(AdminSendErrors.InsufficientPermissions.Description);
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

    private async Task<ErrorOr<string>> ProcessAdminSendAsync(ulong channelId, string message)
    {
        await DeferAsync(ephemeral: true);

        return await adminSendService.SendAsync(new DiscordGuildChannelService(Context.Guild), channelId, message);
    }

    private Task RespondEphemeralAsync(string message) =>
        Context.Interaction.HasResponded ? FollowupAsync(message, ephemeral: true) : RespondAsync(message, ephemeral: true);
}
