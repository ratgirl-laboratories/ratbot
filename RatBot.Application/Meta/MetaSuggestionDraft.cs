namespace RatBot.Application.Meta;

public sealed record MetaSuggestionDraft(
    ulong GuildId,
    ulong AuthorUserId,
    string Title,
    string Summary,
    string Motivation,
    string Specification);