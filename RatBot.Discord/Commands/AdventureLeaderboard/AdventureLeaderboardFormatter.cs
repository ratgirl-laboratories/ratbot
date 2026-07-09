using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public static class AdventureLeaderboardFormatter
{
    private const int MaxDisplayNameLength = 32;

    public static AdventureLeaderboardViewModel Format(
        AdventureEntrySnapshot snapshot,
        int year,
        ImmutableHashSet<ulong> guildMemberUserIds,
        DateTimeOffset lastUpdated
    )
    {
        ImmutableArray<AdventureEntryRow> sortedRows = snapshot
            .Rows.OrderByDescending(row => row.Score)
            .ThenBy(row => IsGuildMember(row, guildMemberUserIds) ? 0 : 1)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ApiOrder)
            .ToImmutableArray();

        ImmutableArray<AdventureLeaderboardViewRow> rows = FormatRows(sortedRows, guildMemberUserIds);

        return new AdventureLeaderboardViewModel(year, snapshot.Rows.Length, rows.Length, lastUpdated, rows);
    }

    private static string BuildProgressBar(IReadOnlyList<AdventureDayProgress> progress)
    {
        StringBuilder text = new StringBuilder(progress.Count);

        foreach (AdventureDayProgress day in progress)
        {
            char symbol = (day.Part1Complete, day.Part2Complete) switch
            {
                (true, true) => '█',
                (true, false) or (false, true) => '▄',
                _ => '▁',
            };

            text.Append(symbol);
        }

        return text.ToString();
    }

    private static string FormatPlainDisplayName(string name)
    {
        string safeName = string.IsNullOrWhiteSpace(name) ? "unknown" : name.Trim();

        if (safeName.Length > MaxDisplayNameLength)
            safeName = safeName[..MaxDisplayNameLength];

        return global::Discord.Format.Sanitize(safeName);
    }

    private static AdventureLeaderboardViewRow FormatRow(AdventureEntryRow row, int rank, ImmutableHashSet<ulong> guildMemberUserIds)
    {
        string displayName =
            ulong.TryParse(row.UserId, out ulong userId) && guildMemberUserIds.Contains(userId)
                ? MentionUtils.MentionUser(userId)
                : FormatPlainDisplayName(row.Name);

        return new AdventureLeaderboardViewRow(rank, row.Score, row.Progress.Count * 2, BuildProgressBar(row.Progress), displayName);
    }

    private static ImmutableArray<AdventureLeaderboardViewRow> FormatRows(
        ImmutableArray<AdventureEntryRow> sortedRows,
        ImmutableHashSet<ulong> guildMemberUserIds
    )
    {
        ImmutableArray<AdventureLeaderboardViewRow>.Builder rows = ImmutableArray.CreateBuilder<AdventureLeaderboardViewRow>(sortedRows.Length);

        int? previousScore = null;
        int currentRank = 0;

        for (int index = 0; index < sortedRows.Length; index++)
        {
            AdventureEntryRow row = sortedRows[index];

            if (previousScore != row.Score)
            {
                currentRank = index + 1;
                previousScore = row.Score;
            }

            rows.Add(FormatRow(row, currentRank, guildMemberUserIds));
        }

        return rows.ToImmutable();
    }

    private static bool IsGuildMember(AdventureEntryRow row, ImmutableHashSet<ulong> guildMemberUserIds) =>
        ulong.TryParse(row.UserId, out ulong userId) && guildMemberUserIds.Contains(userId);
}
