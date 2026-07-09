namespace RatBot.Discord.Features.Logging.Gateway;

public sealed class SerilogBackgroundWorker(DiscordSocketClient discordClient, ILogger logger) : BackgroundService
{
    private const ulong ChannelId = 268882317391429632;
    private const string ImageUrl =
        "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f3/Erithacus_rubecula_with_cocked_head.jpg/1280px-Erithacus_rubecula_with_cocked_head.jpg";
    private const MessageFlags Flags = MessageFlags.ComponentsV2;
    private static readonly AllowedMentions Mentions = new AllowedMentions(AllowedMentionTypes.None);
    private static readonly TimeSpan TtlSpan = TimeSpan.FromMilliseconds('Ñ' << 5);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes('᪃' % 420);
    private readonly ILogger _logger = logger.ForContext<SerilogBackgroundWorker>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WaitForReadyAsync(stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTimeOffset nextSendAt = await PostOnceAsync(stoppingToken).ConfigureAwait(false);
                await DelayUntilAsync(nextSendAt, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("background worker is stopping.");
        }
    }

    private static MessageComponent BuildComponents()
    {
        MediaGalleryBuilder gallery = new MediaGalleryBuilder();
        gallery.AddItem(new MediaGalleryItemProperties { Media = new UnfurledMediaItemProperties(ImageUrl) });

        return new ComponentBuilderV2(
            new ContainerBuilder()
                .WithTextDisplay(new TextDisplayBuilder().WithContent("# *Erithacus rubecula*"))
                .WithSeparator(new SeparatorBuilder())
                .WithMediaGallery(gallery)
                .WithActionRow(
                    new ActionRowBuilder().WithComponents([
                        new ButtonBuilder().WithStyle(ButtonStyle.Primary).WithLabel("Accept?").WithCustomId(SerilogTelemetryHandler.Id),
                    ])
                )
        ).Build();
    }

    private static async Task DelayUntilAsync(DateTimeOffset target, CancellationToken ct)
    {
        TimeSpan delay = target - DateTimeOffset.UtcNow;

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset> PostOnceAsync(CancellationToken ct)
    {
        DateTimeOffset nextSendAt = DateTimeOffset.UtcNow + Interval;
        IUserMessage? message = null;

        try
        {
            if (discordClient.GetChannel(ChannelId) is not IMessageChannel channel)
            {
                _logger.Warning("channel {ChannelId} is unavailable.", ChannelId);
                return nextSendAt;
            }

            message = await channel
                .SendMessageAsync(
                    components: BuildComponents(),
                    flags: Flags,
                    allowedMentions: Mentions,
                    options: new RequestOptions { CancelToken = ct }
                )
                .ConfigureAwait(false);

            nextSendAt = DateTimeOffset.UtcNow + Interval;

            await Task.Delay(TtlSpan, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "post cycle failed.");
        }

        if (message is null)
            return nextSendAt;

        try
        {
            await message.DeleteAsync(new RequestOptions { CancelToken = ct }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "failed to delete {MessageId}.", message.Id);
        }

        return nextSendAt;
    }

    private async Task WaitForReadyAsync(CancellationToken ct)
    {
        if (discordClient.ConnectionState == ConnectionState.Connected)
            return;

        TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        discordClient.Ready += OnReadyAsync;

        try
        {
            if (discordClient.ConnectionState == ConnectionState.Connected)
                return;

            await using CancellationTokenRegistration registration = ct.Register(() => ready.TrySetCanceled(ct));
            await ready.Task.ConfigureAwait(false);
        }
        finally
        {
            discordClient.Ready -= OnReadyAsync;
        }

        return;

        Task OnReadyAsync()
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
