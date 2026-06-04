namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed record AdventureLeaderboardViewRow(
    int Rank,
    int Score,
    int MaxScore,
    string Progress,
    string DisplayName);