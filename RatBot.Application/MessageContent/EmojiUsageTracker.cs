using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RatBot.Application.Common.Interfaces;
using RatBot.Domain.Emoji;

namespace RatBot.Application.MessageContent;

public sealed class EmojiUsageTracker(IEmojiRepository emojiRepository, ITrackedEmojiCatalog trackedEmojiCatalog, ILogger logger)
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

    public async Task RecordMessageBatchUsageAsync(IEnumerable<string> messageContents, CancellationToken ct = default)
    {
        if (!trackedEmojiCatalog.TryGetTrackedEmojiIds(out IReadOnlyCollection<ulong> trackedEmojiIds))
            return;

        HashSet<ulong> trackedEmojiIdSet = trackedEmojiIds.ToHashSet();

        await PruneUntrackedEmojiAsync(trackedEmojiIds, ct).ConfigureAwait(false);

        List<(ulong Id, int N)> usages = messageContents
            .SelectMany(ExtractEmojiIds)
            .Where(trackedEmojiIdSet.Contains)
            .GroupBy(x => x)
            .Select(g => (EmojiId: g.Key, Count: g.Count()))
            .ToList();

        foreach ((ulong emojiId, int count) in usages)
        {
            int updatedRowCount = await emojiRepository
                .EmojiUsageCounts.Where(x => x.EmojiId == emojiId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.MessageUsageCount, x => x.MessageUsageCount + count), ct)
                .ConfigureAwait(false);

            if (updatedRowCount != 0)
                continue;

            emojiRepository.EmojiUsageCounts.Add(
                new EmojiUsageCount
                {
                    EmojiId = emojiId,
                    ReactionUsageCount = 0,
                    MessageUsageCount = count,
                }
            );

            await emojiRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        foreach ((ulong Id, int N) usage in usages)
            _logger.Verbose("Recorded {EmojiUsageCount} message usages for emoji {EmojiId}.", usage.N, usage.Id);
    }

    private Task<int> PruneUntrackedEmojiAsync(IReadOnlyCollection<ulong> trackedEmojiIds, CancellationToken ct) =>
        emojiRepository.EmojiUsageCounts.Where(x => !trackedEmojiIds.Contains(x.EmojiId)).ExecuteDeleteAsync(ct);
}
