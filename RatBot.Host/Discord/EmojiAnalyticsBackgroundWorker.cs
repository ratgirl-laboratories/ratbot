using RatBot.Application.Features.Emoji;

namespace RatBot.Host.Discord;

public sealed class EmojiAnalyticsBackgroundWorker(
    EmojiAnalyticsBuffer buffer,
    IServiceScopeFactory scopeFactory,
    ILogger logger) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<EmojiAnalyticsBackgroundWorker>();

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Emoji analytics background worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Wait for data to be available
                if (!await buffer.Reader.WaitToReadAsync(stoppingToken))
                    break;

                // Collect a batch of items
                Queue<string> emojiBatch = [];

                while (buffer.Reader.TryRead(out string? emojiId))
                {
                    emojiBatch.Enqueue(emojiId);

                    // Stop if batch size is reached (e.g., 100)
                    if (emojiBatch.Count >= 100)
                        break;
                }

                if (emojiBatch.Count > 0)
                    await ProcessBatchAsync(emojiBatch, stoppingToken);

                // Add a small delay if the buffer was empty to avoid tight loop,
                // but only if we didn't just process a full batch.
                if (emojiBatch.Count < 100)
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
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

    private async Task ProcessBatchAsync(Queue<string> emojiBatch, CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            EmojiAnalyticsService emojiAnalyticsService =
                scope.ServiceProvider.GetRequiredService<EmojiAnalyticsService>();

            await emojiAnalyticsService.RecordBatchUsageAsync(emojiBatch, ct);

            _logger.Debug("Processed {Count} emoji usage events from channel.", emojiBatch.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process emoji analytics batch.");
        }
    }
}