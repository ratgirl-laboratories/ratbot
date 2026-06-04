namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed record AdventureLeaderboardSnapshotRow(
    int ApiOrder,
    string UserId,
    string Name,
    string Github,
    IReadOnlyList<AdventureLeaderboardDayProgress> Progress)
{
    public int Score => Progress.Sum(day => (day.Part1Complete ? 1 : 0) + (day.Part2Complete ? 1 : 0));

    public static AdventureLeaderboardSnapshotRow FromDto(AdventureLeaderboardEntryDto dto, int apiOrder)
    {
        List<AdventureLeaderboardDayProgress> progress = (dto.Progress ?? [])
            .Select(AdventureLeaderboardDayProgress.FromPair)
            .ToList();

        return new AdventureLeaderboardSnapshotRow(
            apiOrder,
            dto.UserId ?? string.Empty,
            dto.Name ?? string.Empty,
            dto.Github ?? string.Empty,
            progress);
    }
}