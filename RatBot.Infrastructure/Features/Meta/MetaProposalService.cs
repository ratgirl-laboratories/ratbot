using RatBot.Infrastructure.Features.Meta.Persistence;
using Serilog;

namespace RatBot.Infrastructure.Features.Meta;

public sealed class MetaProposalService(BotDbContext db, ILogger logger)
{
    private static readonly TimeSpan PublicationRetryCooldown = TimeSpan.FromSeconds(10);
    private readonly BotDbContext _db = db;
    private readonly ILogger _logger = logger.ForContext<MetaProposalService>();

    public async Task<ErrorOr<MetaSuggestionSettings>> GetSettingsAsync(ulong guildId, CancellationToken ct = default)
    {
        MetaSuggestionSettings? settings = await _db.MetaSuggestionSettings.AsNoTracking().ForGuild(guildId).SingleOrDefaultAsync(ct);

        if (settings is null)
            return MetaProposalErrors.SettingsNotConfigured;

        ErrorOr<Success> configured = settings.EnsureProposalWorkflowConfigured();

        return configured.IsError ? configured.Errors : settings;
    }

    public async Task TrackSuggestionThreadAsync(
        ulong guildId,
        ulong suggestionThreadChannelId,
        ulong suggestionsForumChannelId,
        ulong originalThreadAuthorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default
    )
    {
        MetaProposalState? existing = await _db
            .MetaProposalStates.AsNoTracking()
            .ForSuggestionThread(suggestionThreadChannelId)
            .SingleOrDefaultAsync(ct);

        if (existing is not null)
            return;

        ErrorOr<MetaProposalState> create = MetaProposalState.TrackSuggestionThread(
            Guid.CreateVersion7(nowUtc),
            guildId,
            suggestionThreadChannelId,
            suggestionsForumChannelId,
            originalThreadAuthorUserId,
            nowUtc
        );

        if (create.IsError)
            return;

        _db.MetaProposalStates.Add(create.Value);
        await _db.SaveChangesAsync(ct);

        _logger.Information("Tracked meta suggestion thread {SuggestionThreadChannelId} in guild {GuildId}.", suggestionThreadChannelId, guildId);
    }

    public async Task ForgetDeletedUnsubmittedSuggestionAsync(ulong suggestionThreadChannelId, CancellationToken ct = default)
    {
        MetaProposalState? state = await _db.MetaProposalStates.ForSuggestionThread(suggestionThreadChannelId).SingleOrDefaultAsync(ct);

        if (state is null)
            return;

        if (state.HasSubmittedProposal || state.IsTerminal)
            return;

        _db.MetaProposalStates.Remove(state);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ErrorOr<MetaProposalState>> GetForAnyThreadAsync(ulong threadChannelId, CancellationToken ct = default) =>
        await FindByAnyThreadAsync(threadChannelId, asNoTracking: true, ct);

    public async Task<ErrorOr<MetaProposalState>> GetForSuggestionThreadAsync(ulong suggestionThreadChannelId, CancellationToken ct = default) =>
        await FindBySuggestionThreadAsync(suggestionThreadChannelId, asNoTracking: true, ct);

    public async Task<ErrorOr<MetaProposalState>> GetByIdAsync(Guid stateId, CancellationToken ct = default)
    {
        return await FindByIdAsync(stateId, asNoTracking: true, ct);
    }

    public async Task<ErrorOr<MetaProposalState>> GetByPollMessageAsync(ulong pollMessageId, CancellationToken ct = default)
    {
        return await FindByPollMessageAsync(pollMessageId, asNoTracking: true, ct);
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
        CancellationToken ct = default
    )
    {
        ErrorOr<MetaProposalState> state = await FindBySuggestionThreadAsync(suggestionThreadChannelId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.SuggestionNotTracked;

        ErrorOr<Success> result = state.Value.StartPoll(
            proposalAuthorUserId,
            title,
            summary,
            motivation,
            specification,
            pollMessageId,
            pollExpiresAtUtc,
            nowUtc
        );

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state.Value;
    }

    public async Task<ErrorOr<MetaProposalState>> CompletePollAsync(Guid stateId, bool submitWon, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.CompletePoll(submitWon);

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state.Value;
    }

    public async Task<ErrorOr<MetaProposalState>> ClearDeletedPollAsync(Guid stateId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.ClearDeletedPoll();

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state.Value;
    }

    public async Task<ErrorOr<MetaProposalState>> ClearDeletedPollByMessageAsync(ulong pollMessageId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByPollMessageAsync(pollMessageId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.ClearDeletedPoll();

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state.Value;
    }

    public async Task RecordPollFinalizationRetryAsync(Guid stateId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return;

        ErrorOr<Success> result = state.Value.RecordPollFinalizationRetry();

        if (result.IsError)
            return;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MetaProposalState>> FindExpiredPollsAsync(DateTimeOffset nowUtc, int limit, CancellationToken ct = default) =>
        await _db.MetaProposalStates.AsNoTracking().PollExpiringBeforeOrAt(nowUtc).OrderBy(x => x.PollExpiresAtUtc).Take(limit).ToListAsync(ct);

    public async Task<ErrorOr<MetaProposalState>> RecordPublishedAsync(Guid stateId, ulong proposalThreadChannelId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.RecordPublished(proposalThreadChannelId);

        if (result.IsError)
            return MetaProposalErrors.PublicationFailed;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> RecordPublicationFailureAsync(
        Guid stateId,
        ulong errorMessageId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default
    )
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.RecordPublicationFailure(errorMessageId, nowUtc);

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> MarkPublicationRetryStartedAsync(
        Guid stateId,
        DateTimeOffset nowUtc,
        CancellationToken ct = default
    )
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        if (state.Value.LastPublicationRetryAtUtc is { } lastRetry && nowUtc - lastRetry < PublicationRetryCooldown)
            return MetaProposalErrors.RetryCooldownActive;

        ErrorOr<Success> result = state.Value.MarkPublicationRetryStarted(nowUtc);

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> VetoAsync(
        ulong threadChannelId,
        ulong vetoedByUserId,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken ct = default
    )
    {
        ErrorOr<MetaProposalState> state = await FindByAnyThreadAsync(threadChannelId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.Veto(vetoedByUserId, reason, nowUtc);

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<MetaProposalState>> CloseSuggestionAsync(Guid stateId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.Close();

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    public async Task<ErrorOr<Success>> ForgetUnsubmittedSuggestionAsync(Guid stateId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        MetaProposalState proposal = state.Value;

        if (proposal.HasSubmittedProposal || proposal.IsTerminal)
            return Error.Conflict("MetaProposal.CannotForgetSubmitted", "Only unsubmitted suggestion state can be forgotten.");

        _db.MetaProposalStates.Remove(proposal);
        await _db.SaveChangesAsync(ct);
        return Result.Success;
    }

    public async Task<ErrorOr<MetaProposalState>> MarkPublishedAsync(Guid stateId, ulong proposalThreadChannelId, CancellationToken ct = default)
    {
        ErrorOr<MetaProposalState> state = await FindByIdAsync(stateId, asNoTracking: false, ct);

        if (state.IsError)
            return MetaProposalErrors.ThreadNotTracked;

        ErrorOr<Success> result = state.Value.RecordPublished(proposalThreadChannelId);

        if (result.IsError)
            return result.Errors;

        await _db.SaveChangesAsync(ct);
        return state;
    }

    private async Task<ErrorOr<MetaProposalState>> FindByIdAsync(Guid stateId, bool asNoTracking, CancellationToken ct)
    {
        IQueryable<MetaProposalState> query = _db.MetaProposalStates.ForId(stateId);
        MetaProposalState? state = await (asNoTracking ? query.AsNoTracking() : query).SingleOrDefaultAsync(ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }

    private async Task<ErrorOr<MetaProposalState>> FindByPollMessageAsync(ulong pollMessageId, bool asNoTracking, CancellationToken ct)
    {
        IQueryable<MetaProposalState> query = _db.MetaProposalStates.ForPollMessage(pollMessageId);
        MetaProposalState? state = await (asNoTracking ? query.AsNoTracking() : query).SingleOrDefaultAsync(ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }

    private async Task<ErrorOr<MetaProposalState>> FindBySuggestionThreadAsync(
        ulong suggestionThreadChannelId,
        bool asNoTracking,
        CancellationToken ct
    )
    {
        IQueryable<MetaProposalState> query = _db.MetaProposalStates.ForSuggestionThread(suggestionThreadChannelId);
        MetaProposalState? state = await (asNoTracking ? query.AsNoTracking() : query).SingleOrDefaultAsync(ct);
        return state is null ? MetaProposalErrors.SuggestionNotTracked : state;
    }

    private async Task<ErrorOr<MetaProposalState>> FindByAnyThreadAsync(ulong threadChannelId, bool asNoTracking, CancellationToken ct)
    {
        IQueryable<MetaProposalState> query = _db.MetaProposalStates.ForAnyThread(threadChannelId);
        MetaProposalState? state = await (asNoTracking ? query.AsNoTracking() : query).SingleOrDefaultAsync(ct);
        return state is null ? MetaProposalErrors.ThreadNotTracked : state;
    }
}
