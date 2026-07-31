namespace RatBot.Application.Common.Interfaces;

public interface ITrackedEmojiCatalog
{
    bool TryGetTrackedEmojiIds(ulong guildId, out IReadOnlyCollection<ulong> emojiIds);
}
