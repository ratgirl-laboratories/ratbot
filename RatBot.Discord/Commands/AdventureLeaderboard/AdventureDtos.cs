using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

#pragma warning disable MA0048, MA0008

public readonly record struct AdventureEntryDto
{
    [JsonPropertyName("github")]
    public string? Github { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("progress")]
    public bool[][]? Progress { get; init; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }
}

public readonly record struct AdventureDayProgress(bool Part1Complete, bool Part2Complete)
{
    public static AdventureDayProgress FromPair(bool[]? pair) =>
        new AdventureDayProgress(pair is { Length: > 0 } && pair[0], pair is { Length: > 1 } && pair[1]);
}

public readonly record struct AdventureEntrySnapshot(
    ImmutableArray<AdventureEntryRow> Rows,
    string Hash)
{
    public static AdventureEntrySnapshot FromDtos(IEnumerable<AdventureEntryDto> rows)
    {
        ImmutableArray<AdventureEntryRow> snapshotRows = rows
            .Select(AdventureEntryRow.FromDto)
            .ToImmutableArray();

        string canonical = BuildCanonicalString(snapshotRows);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        return new AdventureEntrySnapshot(snapshotRows, hash);
    }

    private static string BuildCanonicalString(IReadOnlyList<AdventureEntryRow> rows)
    {
        StringBuilder text = new StringBuilder();

        text.Append("rows=").Append(rows.Count).Append('\n');

        foreach (AdventureEntryRow row in rows)
        {
            text.Append("row=").Append(row.ApiOrder).Append('\n');
            AppendCanonicalString(text, row.UserId);
            AppendCanonicalString(text, row.Name);
            AppendCanonicalString(text, row.Github);
            text.Append("days=").Append(row.Progress.Count).Append('\n');

            foreach (AdventureDayProgress day in row.Progress)
                text.Append(day.Part1Complete ? '1' : '0')
                    .Append(day.Part2Complete ? '1' : '0')
                    .Append('\n');
        }

        return text.ToString();
    }

    private static void AppendCanonicalString(StringBuilder text, string value) =>
        text.Append(value.Length).Append(':').Append(value).Append('\n');
}

public readonly record struct AdventureEntryRow(
    int ApiOrder,
    string UserId,
    string Name,
    string Github,
    IReadOnlyList<AdventureDayProgress> Progress)
{
    public int Score => Progress
        .Sum(day => (day.Part1Complete ? 1 : 0) + (day.Part2Complete ? 1 : 0));

    public static AdventureEntryRow FromDto(AdventureEntryDto dto, int apiOrder)
    {
        List<AdventureDayProgress> progress = (dto.Progress ?? [])
            .Select(AdventureDayProgress.FromPair)
            .ToList();

        return new AdventureEntryRow(
            apiOrder,
            dto.UserId ?? string.Empty,
            dto.Name ?? string.Empty,
            dto.Github ?? string.Empty,
            progress);
    }
}

public readonly record struct AdventureLeaderboardViewModel(
    int Year,
    int TotalEntrants,
    int VisibleEntrants,
    DateTimeOffset LastUpdated,
    IReadOnlyList<AdventureLeaderboardViewRow> Rows);

public readonly record struct AdventureLeaderboardViewRow(
    int Rank,
    int Score,
    int MaxScore,
    string Progress,
    string DisplayName);

public readonly record struct AdventureAccessGrants(ImmutableHashSet<AdventureAccessGrant> Grants);

public readonly record struct AdventureAccessGrant(int ScorePartIndex, ulong ThreadId, ulong UserId);

public readonly record struct AdventureThreadLinkage(int ScorePartIndex, string ThreadName);

public sealed partial class AdventureLeaderboardManager
{
    private readonly record struct TrackedLeaderboardMessageSequence(
        ulong GuildId,
        ulong ChannelId,
        IReadOnlyList<ulong> MessageIds,
        int Year,
        string LastRenderHash);

    private readonly record struct TrackedLeaderboardMessageTarget(
        IGuild Guild,
        ITextChannel Channel,
        IReadOnlyList<IUserMessage> Messages);
}

#pragma warning restore MA0048, MA0008
