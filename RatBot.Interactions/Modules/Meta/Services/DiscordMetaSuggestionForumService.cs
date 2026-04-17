using RatBot.Application.Common.Discord;
using RatBot.Application.Features.Meta.Errors;
using RatBot.Domain.Primitives;

namespace RatBot.Interactions.Modules.Meta.Services;

public sealed class DiscordMetaSuggestionForumService(IGuild guild) : IDiscordMetaSuggestionForumService
{
    public async Task<ErrorOr<CreatedMetaSuggestionThread>> CreateSuggestionThreadAsync(
        ChannelSnowflake forumChannelId,
        string title,
        string firstPost,
        string secondPost,
        string thirdPost)
    {
        IForumChannel? forumChannel = await guild.GetForumChannelAsync(forumChannelId.Id);

        if (forumChannel is null)
            return MetaSuggestionErrors.ForumNotFound;

        IThreadChannel thread = await forumChannel.CreatePostAsync(title, text: firstPost, allowedMentions: AllowedMentions.None);
        await thread.SendMessageAsync(secondPost, allowedMentions: AllowedMentions.None);
        await thread.SendMessageAsync(thirdPost, allowedMentions: AllowedMentions.None);

        return new CreatedMetaSuggestionThread(thread.Id);
    }
}
