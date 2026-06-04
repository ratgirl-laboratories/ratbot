namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed record AdventureLeaderboardDayProgress(bool Part1Complete, bool Part2Complete)
{
    public static AdventureLeaderboardDayProgress FromPair(bool[]? pair) =>
        new AdventureLeaderboardDayProgress(pair is { Length: > 0 } && pair[0], pair is { Length: > 1 } && pair[1]);
}