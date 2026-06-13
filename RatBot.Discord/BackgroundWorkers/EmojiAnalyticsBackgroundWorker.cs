using System.Threading.Channels;
using RatBot.Application.MessageContent;
using RatBot.Application.Reactions;

namespace RatBot.Discord.BackgroundWorkers;

public sealed class EmojiAnalyticsBackgroundWorker(
    ReactionQueue reactionQueue,
    MessageContentQueue messageContentQueue,
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    private const int BatchSize = 100;
    private readonly ILogger _logger = logger.ForContext<EmojiAnalyticsBackgroundWorker>();

    private static Queue<string> DrainBatch(ChannelReader<string> reader)
    {
        Queue<string> batch = new Queue<string>();

        while (reader.TryRead(out string? item))
        {
            batch.Enqueue(item);

            if (batch.Count >= BatchSize)
                break;
        }

        return batch;
    }

    private static Queue<ulong> DrainReactionBatch(ChannelReader<ulong> reader)
    {
        Queue<ulong> batch = new Queue<ulong>();

        while (reader.TryRead(out ulong item))
        {
            batch.Enqueue(item);

            if (batch.Count >= BatchSize)
                break;
        }

        return batch;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Emoji analytics background worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await WaitForDataAsync(stoppingToken).ConfigureAwait(false))
                    break;

                Queue<ulong> reactionBatch = DrainReactionBatch(reactionQueue.Reader);
                Queue<string> messageContentBatch = DrainBatch(messageContentQueue.Reader);

                if (reactionBatch.Count > 0)
                    await ProcessReactionBatchAsync(reactionBatch, stoppingToken).ConfigureAwait(false);

                if (messageContentBatch.Count > 0)
                    await ProcessMessageContentBatchAsync(messageContentBatch, stoppingToken).ConfigureAwait(false);

                if (reactionBatch.Count < BatchSize && messageContentBatch.Count < BatchSize)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Emoji analytics background worker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Emoji analytics background worker encountered an error.");
        }
    }

    private async Task<bool> WaitForDataAsync(CancellationToken ct)
    {
        if (reactionQueue.Reader.TryPeek(out _) || messageContentQueue.Reader.TryPeek(out _))
            return true;

        Task<bool> reactionWaitTask = reactionQueue.Reader.WaitToReadAsync(ct).AsTask();
        Task<bool> messageContentWaitTask = messageContentQueue.Reader.WaitToReadAsync(ct).AsTask();
        Task<bool> completedTask = await Task.WhenAny(reactionWaitTask, messageContentWaitTask).ConfigureAwait(false);

        if (await completedTask.ConfigureAwait(false))
            return true;

        Task<bool> otherTask = ReferenceEquals(completedTask, reactionWaitTask)
            ? messageContentWaitTask
            : reactionWaitTask;

        return await otherTask.ConfigureAwait(false);
    }

    private async Task ProcessReactionBatchAsync(Queue<ulong> emojiBatch, CancellationToken ct)
    {
        try
        {
            AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            await using (scope.ConfigureAwait(false))
            {
                ReactionUsageTracker reactionUsageTracker =
                    scope.ServiceProvider.GetRequiredService<ReactionUsageTracker>();

                await reactionUsageTracker.RecordBatchUsageAsync(emojiBatch, ct).ConfigureAwait(false);

                _logger.Debug("Processed {Count} emoji reaction usage events from channel.", emojiBatch.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process emoji analytics batch.");
        }
    }

    private async Task ProcessMessageContentBatchAsync(Queue<string> messageContentBatch, CancellationToken ct)
    {
        try
        {
            AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            await using (scope.ConfigureAwait(false))
            {
                EmojiUsageTracker emojiUsageTracker =
                    scope.ServiceProvider.GetRequiredService<EmojiUsageTracker>();

                await emojiUsageTracker.RecordMessageBatchUsageAsync(messageContentBatch, ct).ConfigureAwait(false);

                _logger.Debug(
                    "Processed {Count} message content emoji usage events from channel.",
                    messageContentBatch.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process message content emoji analytics batch.");
        }
    }
}