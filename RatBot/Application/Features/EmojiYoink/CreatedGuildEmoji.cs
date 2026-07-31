namespace RatBot.Application.Features.EmojiYoink;

public readonly record struct CreatedGuildEmoji(ulong EmojiId, string Name, bool IsAnimated);
