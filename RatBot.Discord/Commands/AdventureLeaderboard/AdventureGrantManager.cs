using System.Collections.Immutable;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public static class AdventureGrantManager
{
    public static AdventureAccessGrants GenerateAdventureAccessGrants(
        AdventureEntrySnapshot snapshot,
        ImmutableDictionary<int, ulong> threadIdsByScorePart,
        ImmutableHashSet<ulong> adventurerUserIds
    )
    {
        ImmutableHashSet<AdventureAccessGrant>.Builder grants = ImmutableHashSet.CreateBuilder<AdventureAccessGrant>();

        foreach (AdventureEntryRow row in snapshot.Rows)
        {
            if (!ulong.TryParse(row.UserId, out ulong userId))
                continue;

            if (!adventurerUserIds.Contains(userId))
                continue;

            IReadOnlySet<int> completedParts = AdventureScorePart.CompletedParts(row.Progress);

            foreach (int scorePartIndex in completedParts.Order())
                if (threadIdsByScorePart.TryGetValue(scorePartIndex, out ulong threadId))
                    grants.Add(new AdventureAccessGrant(scorePartIndex, threadId, userId));
        }

        return new AdventureAccessGrants(grants.ToImmutable());
    }
}
