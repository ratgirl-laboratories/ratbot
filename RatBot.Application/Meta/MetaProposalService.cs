using RatBot.Application.Common;

namespace RatBot.Application.Meta;

public sealed class MetaProposalService(
    IUnitOfWork uow,
    IMetaProposalRepository proposals,
    ILogger logger)
{
    private static readonly TimeSpan PublicationRetryCooldown = TimeSpan.FromSeconds(10);
    private readonly ILogger _logger = logger.ForContext<MetaProposalService>();

    public async Task<ErrorOr<MetaSuggestionSettings>> GetSettingsAsync(ulong guildId, CancellationToken ct = default)
    {
        _ = ct;
        IRepository<MetaSuggestionSettings> settingsRepo = uow.GetRepository<MetaSuggestionSettings>();
        ErrorOr<MetaSuggestionSettings> settings = await settingsRepo.TryFindAsync((long)guildId);

        if (settings.IsError)
            return MetaProposalErrors.SettingsNotConfigured;

        ErrorOr<Success> configured = settings.Value.EnsureProposalWorkflowConfigured();
        return configured.IsError
            ? configured.Errors
            : settings.Value;
    }

    public async Task<ErrorOr<MetaProposalState>> TrackSuggestionThreadAsync(
        ulong guildId,
        ulong suggestionThreadChannelId,
        ulong suggestionsForumChannelId,
        ulong originalThreadAuthorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        MetaProposalState? existing = await proposals.FindBySuggestionThreadAsync(suggestionThreadChannelId, ct);

        if (existing is not null)
            return existing;

        ErrorOr<MetaProposalState> create = MetaProposalState.TrackSuggestionThread(
            Guid.CreateVersion7(nowUtc),
            guildId,
            suggestionThreadChannelId,
            suggestionsForumChannelId,
            originalThreadAuthorUserId,
            nowUtc);

        if (create.IsError)
            return create.Errors;

        proposals.Add(create.Value);
        await uow.SaveChangesAsync(ct);

        _logger.Information(
            "Tracked meta suggestion thread {SuggestionThreadChannelId} in guild {GuildId}.",
            suggestionThreadChannelId,
            guildId);

        return create.Value;
    }

    public async Task<ErrorOr<Success>> ForgetDeletedUnsubmittedSuggestionAsync(
        ulong suggestionThreadChannelId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindBySuggestionThreadAsync(suggestionThreadChannelId, ct);

        if (state is null)
            return Result.Success;

        if (state.HasSubmittedProposal || state.IsTerminal)
            return Result.Success;

        proposals.Delete(state);
        await uow.SaveChangesAsync(ct);
        return Result.Success;
    }

    public async Task<ErrorOr<MetaProposalState>> GetForSuggestionThreadAsync(
        ulong suggestionThreadChannelId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindBySuggestionThreadAsync(suggestionThreadChannelId, ct);
        return state is null ? MetaProposalErrors.SuggestionNotTracked : state;
    }

    public async Task<ErrorOr<MetaProposalState>> GetForAnyThreadAsync(
        ulong threadChannelId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByProposalThreadAsync(threadChannelId, ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }

    public async Task<ErrorOr<MetaProposalState>> GetByIdAsync(Guid stateId, CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }

    public async Task<ErrorOr<MetaProposalState>> GetByPollMessageAsync(
        ulong pollMessageId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByPollMessageAsync(pollMessageId, ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }

    public async Task<ErrorOr<MetaProposalState>> StartPollAsync(
        ulong suggestionThreadChannelId,
        ulong proposalAuthorUserId,
        string title,
        string summary,
        string motivation,
        string specification,
        ulong pollMessageId,
        DateTimeOffset pollExpiresAtUtc,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindBySuggestionThreadAsync(suggestionThreadChannelId, ct);

        if (state is null)
            return MetaProposalErrors.SuggestionNotTracked;

        ErrorOr<Success> result = state.StartPoll(
            proposalAuthorUserId,
            title,
            summary,
            motivation,
            specification,
            pollMessageId,
            pollExpiresAtUtc,
            nowUtc);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> CompletePollAsync(
        Guid stateId,
        bool submitWon,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.CompletePoll(submitWon);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> ClearDeletedPollAsync(
        Guid stateId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.ClearDeletedPoll();

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> ClearDeletedPollByMessageAsync(
        ulong pollMessageId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByPollMessageAsync(pollMessageId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.ClearDeletedPoll();

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> RecordPollFinalizationRetryAsync(
        Guid stateId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.RecordPollFinalizationRetry();

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public Task<IReadOnlyList<MetaProposalState>> FindExpiredPollsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken ct = default) =>
        proposals.FindExpiredPollsAsync(nowUtc, limit, ct);

    public async Task<ErrorOr<MetaProposalState>> RecordPublishedAsync(
        Guid stateId,
        ulong proposalThreadChannelId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.RecordPublished(proposalThreadChannelId);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> RecordPublicationFailureAsync(
        Guid stateId,
        ulong errorMessageId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.RecordPublicationFailure(errorMessageId, nowUtc);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> MarkPublicationRetryStartedAsync(
        Guid stateId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        if (state.LastPublicationRetryAtUtc is { } lastRetry
            && nowUtc - lastRetry < PublicationRetryCooldown)
            return MetaProposalErrors.RetryCooldownActive;

        ErrorOr<Success> result = state.MarkPublicationRetryStarted(nowUtc);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> VetoAsync(
        ulong threadChannelId,
        ulong vetoedByUserId,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByProposalThreadAsync(threadChannelId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Veto(vetoedByUserId, reason, nowUtc);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> CloseSuggestionAsync(Guid stateId, CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Close();

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<Success>> ForgetUnsubmittedSuggestionAsync(Guid stateId, CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        if (state.HasSubmittedProposal || state.IsTerminal)
            return Error.Conflict(
                "MetaProposal.CannotForgetSubmitted",
                "Only unsubmitted suggestion state can be forgotten.");

        proposals.Delete(state);
        await uow.SaveChangesAsync(ct);
        return Result.Success;
    }

    public async Task<ErrorOr<MetaProposalState>> MarkPublishedAsync(
        Guid stateId,
        ulong proposalThreadChannelId,
        CancellationToken ct = default)
    {
        MetaProposalState? state = await proposals.FindByIdAsync(stateId, ct);

        if (state is null)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.RecordPublished(proposalThreadChannelId);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);
        return state;
    }
}
