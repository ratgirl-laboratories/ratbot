namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardOptions
{
    public const string SectionName = "AdventureLeaderboard";

    private int RefreshIntervalSeconds { get; init; } = 60;

    public string BaseUrl { get; init; } = "https://adventure.practicalpython.org/api/leaderboard/";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Max(10, RefreshIntervalSeconds));
}