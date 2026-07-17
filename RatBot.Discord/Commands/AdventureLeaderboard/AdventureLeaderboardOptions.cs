namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardOptions
{
    public const string SectionName = "AdventureLeaderboard";

    public Dictionary<ulong, AdventureGuildOptions> Guilds { get; init; } = new Dictionary<ulong, AdventureGuildOptions>();

    public string BaseUrl { get; init; } = "https://adventure.practicalpython.org/api/leaderboard/";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(Math.Max(10, RefreshIntervalSeconds));

    private int RefreshIntervalSeconds { get; } = 60;

    public bool TryGetAdventurerRoleId(ulong guildId, out ulong roleId)
    {
        if (!Guilds.TryGetValue(guildId, out AdventureGuildOptions? guildOptions) || guildOptions.AdventurerRoleId == 0)
        {
            roleId = 0;
            return false;
        }

        roleId = guildOptions.AdventurerRoleId;
        return true;
    }
}

public sealed class AdventureGuildOptions
{
    public ulong AdventurerRoleId { get; init; }
}
