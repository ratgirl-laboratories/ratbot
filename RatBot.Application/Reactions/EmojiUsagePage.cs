using RatBot.Domain.Emoji;

namespace RatBot.Application.Reactions;

public sealed record EmojiUsagePage(IReadOnlyList<EmojiUsageCount> Items, int Page, int TotalPages, int TotalCount);
