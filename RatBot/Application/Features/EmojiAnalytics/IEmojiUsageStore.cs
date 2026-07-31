using RatBot.Domain.Emoji;

namespace RatBot.Application.Features.EmojiAnalytics;

public interface IEmojiUsageStore
{
    Task RecordMessageUsageAsync(
        ulong guildId,
        IReadOnlyDictionary<ulong, int> increments,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        CancellationToken ct = default
    );

    Task RecordReactionUsageAsync(
        ulong guildId,
        IReadOnlyDictionary<ulong, int> increments,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        CancellationToken ct = default
    );

    Task<int> CountTrackedUsageAsync(ulong guildId, IReadOnlyCollection<ulong> trackedEmojiIds, CancellationToken ct = default);

    Task<IReadOnlyList<EmojiUsageCount>> GetTrackedUsagePageAsync(
        ulong guildId,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        int offset,
        int limit,
        CancellationToken ct = default
    );
}
