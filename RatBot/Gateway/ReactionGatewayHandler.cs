using RatBot.Application.Reactions;
using RatBot.Commands.Emoji;

namespace RatBot.Gateway;

public sealed class ReactionGatewayHandler(
    DiscordSocketClient discordClient,
    ReactionQueue buffer,
    IOptions<EmojiAnalyticsOptions> options,
    ILogger logger
) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<ReactionGatewayHandler>();
    private readonly EmojiAnalyticsOptions _options = options.Value;

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Subscribe()
    {
        discordClient.ReactionAdded += HandleReactionAddedAsync;
        discordClient.ReactionRemoved += HandleReactionRemovedAsync;
        discordClient.ReactionsCleared += HandleReactionsClearedAsync;
        discordClient.ReactionsRemovedForEmote += HandleReactionsRemovedForEmoteAsync;
    }

    public void Unsubscribe()
    {
        discordClient.ReactionAdded -= HandleReactionAddedAsync;
        discordClient.ReactionRemoved -= HandleReactionRemovedAsync;
        discordClient.ReactionsCleared -= HandleReactionsClearedAsync;
        discordClient.ReactionsRemovedForEmote -= HandleReactionsRemovedForEmoteAsync;
    }

    private async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction
    )
    {
        _ = message;

        if (reaction.Emote is not Emote customEmote)
            return;

        if (!channel.HasValue || channel.Value is not IGuildChannel guildChannel)
        {
            LogReactionEvent("added_ignored_no_guild", reaction.Emote, null);
            return;
        }

        if (!_options.IsEnabled(guildChannel.GuildId))
        {
            LogReactionEvent("added_ignored_disabled", reaction.Emote, guildChannel.GuildId);
            return;
        }

        LogReactionEvent("added", reaction.Emote, guildChannel.GuildId);

        GuildReactionEmoji item = new GuildReactionEmoji(guildChannel.GuildId, customEmote.Id);

        if (!buffer.Writer.TryWrite(item))
            await buffer.Writer.WriteAsync(item);
    }

    private Task HandleReactionRemovedAsync(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction
    )
    {
        _ = cachedMessage;
        ulong? guildId = cachedChannel.HasValue && cachedChannel.Value is IGuildChannel guildChannel ? guildChannel.GuildId : null;
        LogReactionEvent("removed", reaction.Emote, guildId);
        return Task.CompletedTask;
    }

    private Task HandleReactionsClearedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        _ = message;
        ulong? guildId = channel.HasValue && channel.Value is IGuildChannel guildChannel ? guildChannel.GuildId : null;
        _logger.ForContext("ReactionEventType", "cleared_all").ForContext("GuildId", guildId).Information("Discord reaction event recorded.");
        return Task.CompletedTask;
    }

    private Task HandleReactionsRemovedForEmoteAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, IEmote emote)
    {
        _ = message;
        ulong? guildId = channel.HasValue && channel.Value is IGuildChannel guildChannel ? guildChannel.GuildId : null;
        LogReactionEvent("cleared_emote", emote, guildId);
        return Task.CompletedTask;
    }

    private void LogReactionEvent(string reactionEventType, IEmote emote, ulong? guildId)
    {
        ulong? emojiId = emote is Emote customEmote ? customEmote.Id : null;

        _logger
            .ForContext("ReactionEventType", reactionEventType)
            .ForContext("GuildId", guildId)
            .ForContext("EmojiName", emote.Name)
            .ForContext("EmojiId", emojiId)
            .ForContext("IsCustomEmoji", emojiId.HasValue)
            .Debug("Discord reaction event recorded.");
    }
}
