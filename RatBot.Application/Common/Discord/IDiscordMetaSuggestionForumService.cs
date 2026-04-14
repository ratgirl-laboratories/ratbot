namespace RatBot.Application.Common.Discord;

public interface IDiscordMetaSuggestionForumService
{
    Task<ErrorOr<CreatedMetaSuggestionThread>> CreateSuggestionThreadAsync(
        ulong forumChannelId,
        string title,
        string firstPost,
        string secondPost,
        string thirdPost);
}