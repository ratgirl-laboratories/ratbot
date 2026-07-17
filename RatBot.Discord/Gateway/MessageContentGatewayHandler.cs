using RatBot.Application.MessageContent;
using RatBot.Discord.Commands.Emoji;

namespace RatBot.Discord.Gateway;

public sealed class MessageContentGatewayHandler(
    DiscordSocketClient discordClient,
    MessageContentQueue messageContentQueue,
    IOptions<EmojiAnalyticsOptions> options,
    ILogger logger
) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<MessageContentGatewayHandler>();
    private readonly EmojiAnalyticsOptions _options = options.Value;

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe() => discordClient.MessageReceived -= HandleMessageReceivedAsync;

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return;

        if (userMessage.Source != MessageSource.User)
            return;

        if (userMessage.Channel is not SocketGuildChannel guildChannel)
            return;

        if (string.IsNullOrWhiteSpace(userMessage.Content))
            return;

        ulong guildId = guildChannel.Guild.Id;

        if (!_options.IsEnabled(guildId))
            return;

        GuildMessageContent item = new GuildMessageContent(guildId, userMessage.Content);

        if (!messageContentQueue.Writer.TryWrite(item))
            await messageContentQueue.Writer.WriteAsync(item).ConfigureAwait(false);

        _logger.Debug("Queued message content for emoji analytics in guild {GuildId}.", guildId);
    }

    private void Subscribe() => discordClient.MessageReceived += HandleMessageReceivedAsync;
}
