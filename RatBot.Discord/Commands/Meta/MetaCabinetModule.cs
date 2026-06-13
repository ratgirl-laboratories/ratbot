using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Meta;

[Group("meta-cabinet", "Cabinet meta commands.")]
[DefaultMemberPermissions(GuildPermission.BanMembers)]
public sealed class MetaCabinetModule(
    MetaProposalService metaProposalService,
    MetaProposalDiscordWorkflow workflow) : SlashCommandBase
{
    [SlashCommand("propose", "Create a Cabinet proposal poll.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task ProposeAsync(
        [Summary("hours", "Poll duration in hours.")]
        uint hours = MetaCommandIds.DefaultPollHours)
    {
        if (Context.Guild is null || Context.Channel is not IThreadChannel thread)
        {
            await RespondAsync("This command can only be used in a tracked suggestion thread.", ephemeral: true);
            return;
        }

        ErrorOr<MetaSuggestionSettings> settingsResult = await metaProposalService.GetSettingsAsync(Context.Guild.Id);

        if (settingsResult.IsError)
        {
            await RespondAsync(settingsResult.FirstError.Description, ephemeral: true);
            return;
        }

        if (Context.User is not IGuildUser user || !MetaCommandPermissions.IsCabinet(settingsResult.Value, user))
        {
            await RespondAsync("Only Cabinet, Cabinet Chair, or administrators may use this command.", ephemeral: true);
            return;
        }

        if (hours != MetaCommandIds.DefaultPollHours
            && !MetaCommandPermissions.CanOverrideDuration(settingsResult.Value, user))
        {
            await RespondAsync("Only administrators or the Cabinet Chair may override poll duration.", ephemeral: true);
            return;
        }

        if (hours == 0)
        {
            await RespondAsync("Poll duration must be at least 1 hour.", ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> stateResult = await metaProposalService.GetForSuggestionThreadAsync(thread.Id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        string customId = $"{MetaCommandIds.ProposalModalPrefix}:{Context.User.Id}:{thread.Id}:{hours}";
        await Context.Interaction.RespondWithModalAsync<MetaProposalModal>(customId);
    }

    [SlashCommand("veto", "Veto this meta thread.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task VetoAsync()
    {
        if (Context.Guild is null || Context.Channel is not IThreadChannel thread)
        {
            await RespondAsync("This command can only be used in a tracked meta thread.", ephemeral: true);
            return;
        }

        ErrorOr<MetaSuggestionSettings> settingsResult = await metaProposalService.GetSettingsAsync(Context.Guild.Id);

        if (settingsResult.IsError)
        {
            await RespondAsync(settingsResult.FirstError.Description, ephemeral: true);
            return;
        }

        IGuildUser? user = Context.User as IGuildUser;

        if (user is null || !MetaCommandPermissions.IsOwnerOrChair(settingsResult.Value, user))
        {
            await RespondAsync("Only the guild owner, Cabinet Chair, or administrators may veto.", ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> stateResult = await metaProposalService.GetForAnyThreadAsync(thread.Id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        string customId = $"{MetaCommandIds.VetoModalPrefix}:{Context.User.Id}:{thread.Id}";
        await Context.Interaction.RespondWithModalAsync<MetaVetoModal>(customId);
    }

    [ModalInteraction($"{MetaCommandIds.VetoModalPrefix}:*:*", true)]
    public async Task VetoModalAsync(ulong userId, ulong threadId, MetaVetoModal modal)
    {
        if (Context.User.Id != userId)
        {
            await RespondAsync("Only the user who opened this modal can submit it.", ephemeral: true);
            return;
        }

        if (Context.Guild is null || Context.Channel is not IThreadChannel thread || thread.Id != threadId)
        {
            await RespondAsync("This modal can only be submitted in the thread where it was opened.", ephemeral: true);
            return;
        }

        await DeferAsync(true);

        ErrorOr<MetaSuggestionSettings> settingsResult = await metaProposalService.GetSettingsAsync(Context.Guild.Id);

        if (settingsResult.IsError)
        {
            await FollowupAsync(settingsResult.FirstError.Description, ephemeral: true);
            return;
        }

        IGuildUser? user = Context.User as IGuildUser;

        if (user is null || !MetaCommandPermissions.IsOwnerOrChair(settingsResult.Value, user))
        {
            await FollowupAsync("Only the guild owner, Cabinet Chair, or administrators may veto.", ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> vetoResult = await metaProposalService.VetoAsync(
            thread.Id,
            user.Id,
            modal.Reason,
            DateTimeOffset.UtcNow);

        if (vetoResult.IsError)
        {
            await FollowupAsync(vetoResult.FirstError.Description, ephemeral: true);
            return;
        }

        await workflow.PostVetoAsync(vetoResult.Value);
        await FollowupAsync("Veto recorded.", ephemeral: true);
    }
}