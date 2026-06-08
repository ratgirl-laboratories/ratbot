namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardOptions
{
    public const string SectionName = "AdventureLeaderboard";

    public string BaseUrl { get; init; } = "https://adventure.practicalpython.org/api/leaderboard/";

    public ulong AdventurerRoleId { get; init; }

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Max(10, RefreshIntervalSeconds));

    private int RefreshIntervalSeconds { get; init; } = 60;
}
