using System.ComponentModel.DataAnnotations;

namespace RatBot.Discord.Configuration;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    [Required] public string Token { get; init; } = string.Empty;

    [Range(1, ulong.MaxValue)] public ulong GuildId { get; init; }

    [Range(1000, 50000)] public int MessageCacheSize { get; init; } = 5000;
}