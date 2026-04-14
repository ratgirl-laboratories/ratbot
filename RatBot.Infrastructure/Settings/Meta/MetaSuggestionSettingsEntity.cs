namespace RatBot.Infrastructure.Settings.Meta;

public sealed record MetaSuggestionSettingsEntity
{
    public ulong GuildId { get; init; }

    public ulong SuggestForumChannelId { get; init; }
}
