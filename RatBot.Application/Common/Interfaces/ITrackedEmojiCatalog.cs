namespace RatBot.Application.Common.Interfaces;

public interface ITrackedEmojiCatalog
{
    bool TryGetTrackedEmojiIds(out IReadOnlyCollection<ulong> emojiIds);
}
