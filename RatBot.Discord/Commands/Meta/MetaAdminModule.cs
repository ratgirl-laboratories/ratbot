using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Meta;

[Group("meta-admin", "Admin meta commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class MetaAdminModule(MetaProposalService metaProposalService, MetaProposalDiscordWorkflow workflow) : SlashCommandBase
{
    private static string FormatState(MetaProposalState state) =>
        $"""
            Id: `{state.Id}`
            Status: `{state.Status}`
            Guild: `{state.GuildId}`
            Suggestion thread: `{state.SuggestionThreadChannelId}`
            Proposal thread: `{state.ProposalThreadChannelId?.ToString() ?? "none"}`
            Failed poll attempts: `{state.FailedPollAttempts}`
            Poll message: `{state.PollMessageId?.ToString() ?? "none"}`
            Publication retry failures: `{state.PublicationRetryFailures}`
            """;

    [SlashCommand("state", "View meta proposal state.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task StateAsync([Summary("id", "State id.")] string? id = null)
    {
        ErrorOr<MetaProposalState> stateResult = await ResolveStateAsync(id);

        await stateResult.SwitchFirstAsync(
            async state => await RespondAsync(FormatState(state), ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    [SlashCommand("get", "Get the state GUID for the current thread.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task GetAsync()
    {
        ErrorOr<MetaProposalState> stateResult = await metaProposalService.GetForAnyThreadAsync(Context.Channel.Id);

        await stateResult.SwitchFirstAsync(
            async state => await RespondAsync($"State Id: `{state.Id}`", ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    [SlashCommand("retry", "Retry proposal publication.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task RetryAsync([Summary("id", "State id.")] string? id = null)
    {
        ErrorOr<MetaProposalState> stateResult = await ResolveStateAsync(id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        MetaProposalState state = stateResult.Value;
        ErrorOr<MetaSuggestionSettings> settingsResult = await metaProposalService.GetSettingsAsync(state.GuildId);

        if (settingsResult.IsError)
        {
            await RespondAsync(settingsResult.FirstError.Description, ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        ErrorOr<ulong> publishResult = await workflow.PublishProposalAsync(state, settingsResult.Value, pingCabinet: false);

        if (publishResult.IsError)
        {
            await FollowupAsync(publishResult.FirstError.Description, ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> updated = await metaProposalService.RecordPublishedAsync(state.Id, publishResult.Value);

        if (!updated.IsError)
            await workflow.LockArchiveThreadAsync(updated.Value.SuggestionThreadChannelId);

        await updated.SwitchFirstAsync(
            async _ => await FollowupAsync("Proposal marked published.", ephemeral: true),
            async error => await FollowupAsync(error.Description, ephemeral: true)
        );
    }

    [SlashCommand("close", "Terminalize a suggestion.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CloseAsync([Summary("id", "State id.")] string? id = null)
    {
        ErrorOr<MetaProposalState> stateResult = await ResolveStateAsync(id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> closeResult = await metaProposalService.CloseSuggestionAsync(stateResult.Value.Id);

        if (!closeResult.IsError)
            await workflow.LockArchiveThreadAsync(closeResult.Value.SuggestionThreadChannelId);

        await closeResult.SwitchFirstAsync(
            async _ => await RespondAsync("Suggestion terminalized.", ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    [SlashCommand("forget", "Forget unsubmitted state.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ForgetAsync([Summary("id", "State id.")] string? id = null)
    {
        ErrorOr<MetaProposalState> stateResult = await ResolveStateAsync(id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        ErrorOr<Success> forgetResult = await metaProposalService.ForgetUnsubmittedSuggestionAsync(stateResult.Value.Id);

        await forgetResult.SwitchFirstAsync(
            async _ => await RespondAsync("Suggestion state forgotten.", ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    [SlashCommand("publish", "Mark as manually published.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task PublishAsync(IThreadChannel thread, [Summary("id", "State id.")] string? id = null)
    {
        ErrorOr<MetaProposalState> stateResult = await ResolveStateAsync(id);

        if (stateResult.IsError)
        {
            await RespondAsync(stateResult.FirstError.Description, ephemeral: true);
            return;
        }

        ErrorOr<MetaProposalState> updated = await metaProposalService.MarkPublishedAsync(stateResult.Value.Id, thread.Id);

        if (!updated.IsError)
            await workflow.LockArchiveThreadAsync(updated.Value.SuggestionThreadChannelId);

        await updated.SwitchFirstAsync(
            async _ => await RespondAsync("Proposal marked published.", ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    private async Task<ErrorOr<MetaProposalState>> ResolveStateAsync(string? id)
    {
        if (id is not null)
            return await FindStateAsync(id);

        return await metaProposalService.GetForAnyThreadAsync(Context.Channel.Id);
    }

    private Task<ErrorOr<MetaProposalState>> FindStateAsync(string id) =>
        Guid.TryParse(id, out Guid stateId)
            ? metaProposalService.GetByIdAsync(stateId)
            : Task.FromResult<ErrorOr<MetaProposalState>>(Error.Validation("MetaProposal.InvalidStateId", "State id must be a GUID."));
}
