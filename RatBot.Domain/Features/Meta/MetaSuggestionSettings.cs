using RatBot.Domain.Primitives;

namespace RatBot.Domain.Features.Meta;

public sealed record MetaSuggestionSettings(GuildSnowflake GuildId, ChannelSnowflake SuggestForumChannelId);