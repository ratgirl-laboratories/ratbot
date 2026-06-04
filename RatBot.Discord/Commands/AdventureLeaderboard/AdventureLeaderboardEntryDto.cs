using System.Text.Json.Serialization;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardEntryDto
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
