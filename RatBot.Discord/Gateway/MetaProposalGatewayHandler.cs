using RatBot.Application.Meta;
using RatBot.Discord.BackgroundWorkers;

namespace RatBot.Discord.Gateway;

public sealed class MetaProposalGatewayHandler(
    DiscordSocketClient discordClient,
    IServiceScopeFactory scopeFactory,
    MetaProposalPollResolver pollResolver,
    ILogger logger) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<MetaProposalGatewayHandler>();

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe()
    {
        discordClient.ThreadCreated -= HandleThreadCreatedAsync;
        discordClient.ThreadDeleted -= HandleThreadDeletedAsync;
        discordClient.MessageDeleted -= HandleMessageDeletedAsync;
        discordClient.MessageUpdated -= HandleMessageUpdatedAsync;
    }

    private void Subscribe()
    {
        discordClient.ThreadCreated += HandleThreadCreatedAsync;
        discordClient.ThreadDeleted += HandleThreadDeletedAsync;
        discordClient.MessageDeleted += HandleMessageDeletedAsync;
        discordClient.MessageUpdated += HandleMessageUpdatedAsync;
    }

    private async Task HandleThreadCreatedAsync(SocketThreadChannel thread)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            MetaProposalService service = scope.ServiceProvider.GetRequiredService<MetaProposalService>();
            ErrorOr<MetaSuggestionSettings> settingsResult = await service.GetSettingsAsync(thread.Guild.Id);

            if (settingsResult.IsError)
                return;

            MetaSuggestionSettings settings = settingsResult.Value;

            if (thread.ParentChannel.Id != settings.SuggestionsForumChannelId)
                return;

            ulong ownerId = ((IThreadChannel)thread).OwnerId;

            if (ownerId == 0)
            {
                _logger.Warning(
                    "Ignoring meta suggestion thread {ThreadId} because owner id was unavailable.",
                    thread.Id);
                return;
            }

            await service.TrackSuggestionThreadAsync(
                thread.Guild.Id,
                thread.Id,
                settings.SuggestionsForumChannelId,
                ownerId,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to handle meta suggestion thread creation event.");
        }
    }

    private async Task HandleThreadDeletedAsync(Cacheable<SocketThreadChannel, ulong> thread)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            MetaProposalService service = scope.ServiceProvider.GetRequiredService<MetaProposalService>();
            await service.ForgetDeletedUnsubmittedSuggestionAsync(thread.Id);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to handle meta suggestion thread deletion event.");
        }
    }

    private async Task HandleMessageDeletedAsync(
        Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        _ = channel;

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            MetaProposalService service = scope.ServiceProvider.GetRequiredService<MetaProposalService>();
            ErrorOr<MetaProposalState> clearResult = await service.ClearDeletedPollByMessageAsync(message.Id);

            if (!clearResult.IsError)
            {
                _logger.Information(
                    "Cleared deleted meta proposal poll message {PollMessageId} for state {StateId}.",
                    message.Id,
                    clearResult.Value.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to handle meta proposal poll deletion event.");
        }
    }

    private async Task HandleMessageUpdatedAsync(
        Cacheable<IMessage, ulong> cachedMessage,
        SocketMessage updatedMessage,
        ISocketMessageChannel channel)
    {
        _ = cachedMessage;
        _ = channel;

        if (updatedMessage is not IUserMessage pollMessage)
            return;

        Poll? pollValue = pollMessage.Poll;

        if (pollValue is not { } poll || poll.Results is not { IsFinalized: true })
            return;

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            MetaProposalService service = scope.ServiceProvider.GetRequiredService<MetaProposalService>();
            ErrorOr<MetaProposalState> stateResult = await service.GetByPollMessageAsync(pollMessage.Id);

            if (stateResult.IsError || stateResult.Value.Status is not MetaProposalStatus.PollActive)
                return;

            await pollResolver.ResolveFinalizedPollAsync(
                service,
                stateResult.Value,
                pollMessage,
                CancellationToken.None);

            _logger.Information(
                "Resolved manually finalized meta proposal poll {PollMessageId} for state {StateId}.",
                pollMessage.Id,
                stateResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to handle manually finalized meta proposal poll update.");
        }
    }
}
