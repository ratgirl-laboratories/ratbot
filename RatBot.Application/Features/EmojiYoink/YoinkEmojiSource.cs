namespace RatBot.Application.Features.EmojiYoink;

public readonly record struct YoinkEmojiSource(ulong EmojiId, string InvokedName, bool IsAnimated);
