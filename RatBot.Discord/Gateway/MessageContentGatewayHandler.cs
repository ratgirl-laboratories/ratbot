using RatBot.Application.MessageContent;

namespace RatBot.Discord.Gateway;

public sealed class MessageContentGatewayHandler(DiscordSocketClient discordClient, MessageContentQueue messageContentQueue, ILogger logger)
    : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<MessageContentGatewayHandler>();

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe() => discordClient.MessageReceived -= HandleMessageReceivedAsync;

    private void Subscribe() => discordClient.MessageReceived += HandleMessageReceivedAsync;

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return;

        if (userMessage.Source != MessageSource.User)
            return;

        if (string.IsNullOrWhiteSpace(userMessage.Content))
            return;

        if (!messageContentQueue.Writer.TryWrite(userMessage.Content))
            await messageContentQueue.Writer.WriteAsync(userMessage.Content).ConfigureAwait(false);

        _logger.Debug("Queued message content for emoji analytics.");
    }
}
