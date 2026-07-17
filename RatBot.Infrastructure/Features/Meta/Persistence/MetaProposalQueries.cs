namespace RatBot.Infrastructure.Features.Meta.Persistence;

internal static class MetaProposalQueries
{
    public static IQueryable<MetaProposalState> ForAnyThread(this IQueryable<MetaProposalState> query, ulong guildId, ulong threadChannelId) =>
        query.Where(x => x.GuildId == guildId && (x.SuggestionThreadChannelId == threadChannelId || x.ProposalThreadChannelId == threadChannelId));

    public static IQueryable<MetaSuggestionSettings> ForGuild(this IQueryable<MetaSuggestionSettings> query, ulong guildId) =>
        query.Where(x => x.GuildId == guildId);

    public static IQueryable<MetaProposalState> ForId(this IQueryable<MetaProposalState> query, Guid stateId) => query.Where(x => x.Id == stateId);

    public static IQueryable<MetaProposalState> ForPollMessage(this IQueryable<MetaProposalState> query, ulong guildId, ulong pollMessageId) =>
        query.Where(x => x.GuildId == guildId && x.PollMessageId == pollMessageId);

    public static IQueryable<MetaProposalState> ForSuggestionThread(
        this IQueryable<MetaProposalState> query,
        ulong guildId,
        ulong suggestionThreadChannelId
    ) => query.Where(x => x.GuildId == guildId && x.SuggestionThreadChannelId == suggestionThreadChannelId);

    public static IQueryable<MetaProposalState> PollExpiringBeforeOrAt(this IQueryable<MetaProposalState> query, DateTimeOffset nowUtc) =>
        Queryable.Where(
            query,
            x => x.Status == MetaProposalStatus.PollActive && x.PollMessageId.HasValue && x.PollExpiresAtUtc.HasValue && x.PollExpiresAtUtc <= nowUtc
        );
}
