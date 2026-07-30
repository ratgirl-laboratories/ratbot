using RatBot.Application.Features.EmojiYoink;
using RatBot.Commands;

namespace RatBot.Features.EmojiYoink.Commands;

[DefaultMemberPermissions(GuildPermission.ManageEmojisAndStickers)]
[RequireUserPermission(GuildPermission.ManageEmojisAndStickers)]
[RequireBotPermission(GuildPermission.CreateGuildExpressions)]
[CommandContextType(InteractionContextType.Guild)]
public sealed class EmojiYoinkModule(EmojiYoinkOperations operations) : SlashCommandBase
{
    [SlashCommand("yoink", "Copy a custom emoji into this server.")]
    public async Task YoinkAsync([Summary("emoji", "The custom emoji to copy.")] string emoji)
    {
        ErrorOr<YoinkEmojiSource> sourceResult = CustomEmojiMentionParser.Parse(emoji);

        await ExecuteAsync(sourceResult, rejectLocalSource: false).ConfigureAwait(false);
    }

    [MessageCommand("Yoink emoji")]
    public async Task YoinkMessageAsync(IMessage message)
    {
        ErrorOr<YoinkEmojiSource> sourceResult = CustomEmojiMentionParser.Parse(message.Content);

        await ExecuteAsync(sourceResult, rejectLocalSource: true).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(ErrorOr<YoinkEmojiSource> sourceResult, bool rejectLocalSource)
    {
        SocketGuild? guild = Context.Guild;

        if (guild is null)
        {
            await RespondAsync(EmojiYoinkErrors.GuildOnly.Description).ConfigureAwait(false);
            return;
        }

        if (sourceResult.IsError)
        {
            await RespondAsync(sourceResult.FirstError.Description).ConfigureAwait(false);
            return;
        }

        YoinkEmojiSource source = sourceResult.Value;

        if (rejectLocalSource && guild.Emotes.Any(emote => emote.Id == source.EmojiId))
        {
            await RespondAsync(EmojiYoinkErrors.SourceAlreadyInGuild.Description).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);

        ErrorOr<CreatedGuildEmoji> result = await operations
            .YoinkAsync(new YoinkEmojiCommand(guild.Id, Context.User.Id, Context.User.Username, source), CancellationToken.None)
            .ConfigureAwait(false);

        await result.SwitchFirstAsync(
            async created =>
            {
                string prefix = created.IsAnimated ? "a" : string.Empty;

                await FollowupAsync($"Added emoji <{prefix}:{created.Name}:{created.EmojiId}>.").ConfigureAwait(false);
            },
            async error => await FollowupAsync(error.Description).ConfigureAwait(false)
        );
    }
}
