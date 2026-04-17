namespace RatBot.Application.Features.Meta.Models;

public sealed record MetaSuggestionDraft(
    ulong GuildId,
    ulong AuthorUserId,
    string Title,
    string Summary,
    string Motivation,
    string Specification);
