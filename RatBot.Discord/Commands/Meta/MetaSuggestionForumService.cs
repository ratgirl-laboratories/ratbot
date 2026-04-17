using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Meta;

public sealed class MetaSuggestionForumService(IGuild guild) : ISuggestionThreadPublisher
{
    public async Task<ErrorOr<PublishedSuggestionThread>> PublishSuggestionThreadAsync(
        ulong forumChannelId,
        string title,
        string firstPost,
        string secondPost,
        string thirdPost)
    {
        IForumChannel? forumChannel = await guild.GetForumChannelAsync(forumChannelId);

        if (forumChannel is null)
            return MetaSuggestionErrors.ForumNotFound;

        IThreadChannel thread = await forumChannel.CreatePostAsync(
            title,
            text: firstPost,
            allowedMentions: AllowedMentions.None);

        await thread.SendMessageAsync(secondPost, allowedMentions: AllowedMentions.None);
        await thread.SendMessageAsync(thirdPost, allowedMentions: AllowedMentions.None);

        return new PublishedSuggestionThread(thread.Id);
    }
}
