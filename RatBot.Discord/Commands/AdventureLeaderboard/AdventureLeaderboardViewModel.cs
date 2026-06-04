namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed record AdventureLeaderboardViewModel(
    int Year,
    int TotalEntrants,
    int VisibleEntrants,
    DateTimeOffset LastUpdated,
    IReadOnlyList<AdventureLeaderboardViewRow> Rows);