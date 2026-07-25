using System.Globalization;
using System.Text.RegularExpressions;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Features.EmojiAnalytics;

namespace RatBot.Application.MessageContent;

public sealed class EmojiUsageTracker(IEmojiUsageStore emojiUsageStore, ITrackedEmojiCatalog trackedEmojiCatalog, ILogger logger)
{
    private static readonly Regex EmojiRegex = new Regex(
        @"<a?:\w{2,32}:(?<id>\d{17,21})>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    private readonly ILogger _logger = logger.ForContext<EmojiUsageTracker>();

    private static IEnumerable<ulong> ExtractEmojiIds(string messageContent) =>
        EmojiRegex
            .Matches(messageContent)
            .Select(match => match.Groups["id"].Value)
            .Select(TryParseEmojiId)
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault());

    private static ulong? TryParseEmojiId(string id) =>
        ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out ulong emojiId) ? emojiId : null;

    public async Task RecordMessageBatchUsageAsync(ulong guildId, IEnumerable<string> messageContents, CancellationToken ct = default)
    {
        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(guildId, out IReadOnlyCollection<ulong> trackedEmojiIds))
            return;

        HashSet<ulong> trackedEmojiIdSet = trackedEmojiIds.ToHashSet();

        Dictionary<ulong, int> usages = messageContents
            .SelectMany(ExtractEmojiIds)
            .Where(trackedEmojiIdSet.Contains)
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        await emojiUsageStore.RecordMessageUsageAsync(guildId, usages, trackedEmojiIds, ct).ConfigureAwait(false);

        foreach ((ulong emojiId, int count) in usages)
            _logger.Verbose("Recorded {EmojiUsageCount} message usages for emoji {EmojiId}.", count, emojiId);
    }
}
