using RatBot.Domain.Primitives;

namespace RatBot.Application.Common.Discord;

public interface IDiscordMetaSuggestionForumService
{
    Task<ErrorOr<CreatedMetaSuggestionThread>> CreateSuggestionThreadAsync(
        ChannelSnowflake forumChannelId,
        string title,
        string firstPost,
        string secondPost,
        string thirdPost);
}