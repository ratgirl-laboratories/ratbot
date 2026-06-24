using RatBot.Application.Reactions;

namespace RatBot.Discord.Gateway;

public sealed class ReactionGatewayHandler(DiscordSocketClient discordClient, ReactionQueue buffer, ILogger logger) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<ReactionGatewayHandler>();

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe()
    {
        discordClient.ReactionAdded -= HandleReactionAddedAsync;
        discordClient.ReactionRemoved -= HandleReactionRemovedAsync;
        discordClient.ReactionsCleared -= HandleReactionsClearedAsync;
        discordClient.ReactionsRemovedForEmote -= HandleReactionsRemovedForEmoteAsync;
    }

    public void Subscribe()
    {
        discordClient.ReactionAdded += HandleReactionAddedAsync;
        discordClient.ReactionRemoved += HandleReactionRemovedAsync;
        discordClient.ReactionsCleared += HandleReactionsClearedAsync;
        discordClient.ReactionsRemovedForEmote += HandleReactionsRemovedForEmoteAsync;
    }

    private async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction
    )
    {
        _ = message;
        _ = channel;

        LogReactionEvent("added", reaction.Emote);

        if (reaction.Emote is not Emote customEmote)
            return;

        if (!buffer.Writer.TryWrite(customEmote.Id))
            await buffer.Writer.WriteAsync(customEmote.Id);
    }

    private Task HandleReactionRemovedAsync(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction
    )
    {
        _ = cachedMessage;
        _ = cachedChannel;
        LogReactionEvent("removed", reaction.Emote);
        return Task.CompletedTask;
    }

    private Task HandleReactionsClearedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        _ = message;
        _ = channel;
        _logger.ForContext("ReactionEventType", "cleared_all").Information("Discord reaction event recorded.");
        return Task.CompletedTask;
    }

    private Task HandleReactionsRemovedForEmoteAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, IEmote emote)
    {
        _ = message;
        _ = channel;
        LogReactionEvent("cleared_emote", emote);
        return Task.CompletedTask;
    }

    private void LogReactionEvent(string reactionEventType, IEmote emote)
    {
        ulong? emojiId = emote is Emote customEmote ? customEmote.Id : null;

        _logger
            .ForContext("ReactionEventType", reactionEventType)
            .ForContext("EmojiName", emote.Name)
            .ForContext("EmojiId", emojiId)
            .ForContext("IsCustomEmoji", emojiId.HasValue)
            .Debug("Discord reaction event recorded.");
    }
}
