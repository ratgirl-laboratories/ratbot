using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public static class AdventureLeaderboardFormatter
{
    private const int MaxVisibleRows = 25;
    private const int MaxDisplayNameLength = 32;

    public static AdventureLeaderboardViewModel Format(
        AdventureLeaderboardSnapshot snapshot,
        int year,
        IReadOnlySet<ulong> guildMemberUserIds,
        DateTimeOffset lastUpdated)
    {
        List<AdventureLeaderboardViewRow> rows = snapshot.Rows
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.ApiOrder)
            .Take(MaxVisibleRows)
            .Select((row, index) => FormatRow(row, index + 1, guildMemberUserIds))
            .ToList();

        return new AdventureLeaderboardViewModel(year, snapshot.Rows.Count, rows.Count, lastUpdated, rows);
    }

    private static string BuildProgressBar(IReadOnlyList<AdventureLeaderboardDayProgress> progress)
    {
        StringBuilder text = new StringBuilder(progress.Count);

        foreach (AdventureLeaderboardDayProgress day in progress)
        {
            char symbol = (day.Part1Complete, day.Part2Complete) switch
            {
                (true, true) => '●',
                (true, false) or (false, true) => '◐',
                _ => '○',
            };

            text.Append(symbol);
        }

        return text.ToString();
    }

    private static string FormatPlainDisplayName(string name)
    {
        string safeName = string.IsNullOrWhiteSpace(name)
            ? "unknown"
            : name.Trim();

        if (safeName.Length > MaxDisplayNameLength)
            safeName = safeName[..MaxDisplayNameLength];

        return global::Discord.Format.Sanitize(safeName);
    }

    private static AdventureLeaderboardViewRow FormatRow(
        AdventureLeaderboardSnapshotRow row,
        int rank,
        IReadOnlySet<ulong> guildMemberUserIds)
    {
        string displayName = ulong.TryParse(row.UserId, out ulong userId) && guildMemberUserIds.Contains(userId)
            ? MentionUtils.MentionUser(userId)
            : FormatPlainDisplayName(row.Name);

        return new AdventureLeaderboardViewRow(
            rank,
            row.Score,
            row.Progress.Count * 2,
            BuildProgressBar(row.Progress),
            displayName);
    }
}