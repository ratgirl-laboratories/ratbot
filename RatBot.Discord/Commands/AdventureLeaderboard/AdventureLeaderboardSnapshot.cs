using System.Security.Cryptography;
using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed record AdventureLeaderboardSnapshot(
    IReadOnlyList<AdventureLeaderboardSnapshotRow> Rows,
    string Hash)
{
    public static AdventureLeaderboardSnapshot FromDtos(IEnumerable<AdventureLeaderboardEntryDto> rows)
    {
        List<AdventureLeaderboardSnapshotRow> snapshotRows = rows
            .Select((row, index) => AdventureLeaderboardSnapshotRow.FromDto(row, index))
            .ToList();

        string canonical = BuildCanonicalString(snapshotRows);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        return new AdventureLeaderboardSnapshot(snapshotRows, hash);
    }

    private static string BuildCanonicalString(IReadOnlyList<AdventureLeaderboardSnapshotRow> rows)
    {
        StringBuilder text = new StringBuilder();

        text.Append("rows=").Append(rows.Count).Append('\n');

        foreach (AdventureLeaderboardSnapshotRow row in rows)
        {
            text.Append("row=").Append(row.ApiOrder).Append('\n');
            AppendCanonicalString(text, row.UserId);
            AppendCanonicalString(text, row.Name);
            AppendCanonicalString(text, row.Github);
            text.Append("days=").Append(row.Progress.Count).Append('\n');

            foreach (AdventureLeaderboardDayProgress day in row.Progress)
                text.Append(day.Part1Complete ? '1' : '0').Append(day.Part2Complete ? '1' : '0').Append('\n');
        }

        return text.ToString();
    }

    private static void AppendCanonicalString(StringBuilder text, string value) =>
        text.Append(value.Length).Append(':').Append(value).Append('\n');
}