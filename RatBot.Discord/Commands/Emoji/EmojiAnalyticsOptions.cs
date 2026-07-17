namespace RatBot.Discord.Commands.Emoji;

public sealed class EmojiAnalyticsOptions
{
    public const string SectionName = "EmojiAnalytics";

    public ulong[] EnabledGuildIds { get; init; } = [];

    public bool IsEnabled(ulong guildId) => EnabledGuildIds.Contains(guildId);
}
