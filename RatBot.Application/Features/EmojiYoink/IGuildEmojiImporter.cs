namespace RatBot.Application.Features.EmojiYoink;

public interface IGuildEmojiImporter
{
    Task<ErrorOr<CreatedGuildEmoji>> ImportAsync(
        ulong guildId,
        ulong moderatorUserId,
        EmojiName destinationName,
        YoinkEmojiSource source,
        CancellationToken ct
    );
}
