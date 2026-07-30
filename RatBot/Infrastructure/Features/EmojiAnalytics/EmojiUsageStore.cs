using Microsoft.EntityFrameworkCore;
using RatBot.Application.Features.EmojiAnalytics;
using RatBot.Domain.Emoji;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Features.EmojiAnalytics;

public sealed class EmojiUsageStore(BotDbContext db) : IEmojiUsageStore
{
    public async Task RecordMessageUsageAsync(
        ulong guildId,
        IReadOnlyDictionary<ulong, int> increments,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        CancellationToken ct = default
    )
    {
        await db
            .EmojiUsageCounts.Where(x => x.GuildId == guildId && !trackedEmojiIds.Contains(x.EmojiId))
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        List<ulong> incrementEmojiIds = increments.Keys.ToList();

        EmojiUsageCount[] existingRows = await db
            .EmojiUsageCounts.Where(x => x.GuildId == guildId && incrementEmojiIds.Contains(x.EmojiId))
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        Dictionary<ulong, EmojiUsageCount> existingRowsByEmojiId = existingRows.ToDictionary(x => x.EmojiId);

        foreach ((ulong emojiId, int increment) in increments)
        {
            if (existingRowsByEmojiId.TryGetValue(emojiId, out EmojiUsageCount? row))
            {
                row.MessageUsageCount += increment;
                continue;
            }

            db.EmojiUsageCounts.Add(
                new EmojiUsageCount
                {
                    GuildId = guildId,
                    EmojiId = emojiId,
                    MessageUsageCount = increment,
                    ReactionUsageCount = 0,
                }
            );
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RecordReactionUsageAsync(
        ulong guildId,
        IReadOnlyDictionary<ulong, int> increments,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        CancellationToken ct = default
    )
    {
        await db
            .EmojiUsageCounts.Where(x => x.GuildId == guildId && !trackedEmojiIds.Contains(x.EmojiId))
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        ulong[] incrementEmojiIds = increments.Keys.ToArray();
        List<EmojiUsageCount> existingRows = await db
            .EmojiUsageCounts.Where(x => x.GuildId == guildId && incrementEmojiIds.Contains(x.EmojiId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        Dictionary<ulong, EmojiUsageCount> existingRowsByEmojiId = existingRows.ToDictionary(x => x.EmojiId);

        foreach ((ulong emojiId, int increment) in increments)
        {
            if (existingRowsByEmojiId.TryGetValue(emojiId, out EmojiUsageCount? row))
            {
                row.ReactionUsageCount += increment;
                continue;
            }

            db.EmojiUsageCounts.Add(
                new EmojiUsageCount
                {
                    GuildId = guildId,
                    EmojiId = emojiId,
                    MessageUsageCount = 0,
                    ReactionUsageCount = increment,
                }
            );
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<int> CountTrackedUsageAsync(ulong guildId, IReadOnlyCollection<ulong> trackedEmojiIds, CancellationToken ct = default) =>
        db.EmojiUsageCounts.AsNoTracking().CountAsync(x => x.GuildId == guildId && trackedEmojiIds.Contains(x.EmojiId), ct);

    public async Task<IReadOnlyList<EmojiUsageCount>> GetTrackedUsagePageAsync(
        ulong guildId,
        IReadOnlyCollection<ulong> trackedEmojiIds,
        int offset,
        int limit,
        CancellationToken ct = default
    ) =>
        await db
            .EmojiUsageCounts.AsNoTracking()
            .Where(x => x.GuildId == guildId && trackedEmojiIds.Contains(x.EmojiId))
            .OrderByDescending(x => x.ReactionUsageCount + x.MessageUsageCount)
            .ThenBy(x => x.EmojiId)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
