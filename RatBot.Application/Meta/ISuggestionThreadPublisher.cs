namespace RatBot.Application.Meta;

public interface ISuggestionThreadPublisher
{
    Task<ErrorOr<PublishedSuggestionThread>> PublishSuggestionThreadAsync(
        ulong forumChannelId,
        string title,
        string firstPost,
        string secondPost,
        string thirdPost
    );
}