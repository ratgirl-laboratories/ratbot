namespace RatBot.Application.Features.EmojiYoink;

public readonly record struct YoinkEmojiCommand(ulong GuildId, ulong ModeratorUserId, string ModeratorUsername, YoinkEmojiSource Source);
