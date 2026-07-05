using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using RatBot.Application.Features.Logging;
using RatBot.Discord.Gateway;
using RatBot.Domain.Features.Logging;
using RatBot.Infrastructure.Features.Logging;

namespace RatBot.Discord.Features.Logging.Gateway;

public sealed class ModerationLoggingGatewayHandler(
    DiscordSocketClient discordClient,
    MessageEvidenceCache evidenceCache,
    ModerationLoggingStore loggingStore,
    HttpClient httpClient,
    IOptions<LoggingOptions> options,
    ILogger logger
) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<ModerationLoggingGatewayHandler>();
    private readonly LoggingOptions _options = options.Value;

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe()
    {
        discordClient.MessageReceived -= HandleMessageReceivedAsync;
        discordClient.MessageUpdated -= HandleMessageUpdatedAsync;
        discordClient.MessageDeleted -= HandleMessageDeletedAsync;
        discordClient.MessagesBulkDeleted -= HandleMessagesBulkDeletedAsync;
    }

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return;

        if (userMessage.Source != MessageSource.User || userMessage.Author.IsBot || userMessage.Author.IsWebhook)
            return;

        if (!TryGetGuildId(userMessage.Channel, out ulong guildId))
            return;

        try
        {
            LoggingConfiguration configuration = await loggingStore.GetConfigurationAsync(guildId, CancellationToken.None).ConfigureAwait(false);

            if (!configuration.Enabled)
                return;

            bool isExcluded = await loggingStore.IsExcludedAsync(guildId, userMessage.Channel.Id, CancellationToken.None).ConfigureAwait(false);

            if (isExcluded)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            await loggingStore
                .ObserveMessageAsync(
                    new ObservedMessage(userMessage.Id, guildId, userMessage.Channel.Id, userMessage.Author.Id, now),
                    CancellationToken.None
                )
                .ConfigureAwait(false);

            MessageEvidence evidence = await CreateEvidenceAsync(guildId, userMessage, now, CancellationToken.None).ConfigureAwait(false);
            evidenceCache.Put(evidence, now, configuration.EvidenceRetentionPeriod);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to observe message {MessageId} for moderation logging.", userMessage.Id);
        }
    }

    private async Task HandleMessageUpdatedAsync(
        Cacheable<IMessage, ulong> cachedMessage,
        SocketMessage updatedMessage,
        ISocketMessageChannel channel
    )
    {
        if (updatedMessage is not SocketUserMessage userMessage)
            return;

        if (!TryGetGuildId(channel, out ulong guildId))
            return;

        try
        {
            LoggingConfiguration configuration = await loggingStore.GetConfigurationAsync(guildId, CancellationToken.None).ConfigureAwait(false);

            if (!configuration.Enabled)
                return;

            bool isExcluded = await loggingStore.IsExcludedAsync(guildId, channel.Id, CancellationToken.None).ConfigureAwait(false);

            if (isExcluded)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            evidenceCache.TryGet(userMessage.Id, now, out MessageEvidence before);

            MessageEvidence after = await CreateEvidenceAsync(guildId, userMessage, now, CancellationToken.None).ConfigureAwait(false);
            IUserMessage? logMessage = await SendLogAsync(
                    configuration,
                    LoggingEventKind.Edit,
                    BuildEditText(userMessage, before, after),
                    before.Attachments
                )
                .ConfigureAwait(false);

            if (logMessage is not null)
                await loggingStore
                    .RecordLogEntriesAsync(new[] { new MessageLogEntry(userMessage.Id, logMessage.Id, now) }, CancellationToken.None)
                    .ConfigureAwait(false);

            await loggingStore
                .ObserveMessageAsync(new ObservedMessage(userMessage.Id, guildId, channel.Id, userMessage.Author.Id, now), CancellationToken.None)
                .ConfigureAwait(false);

            evidenceCache.Put(after, now, configuration.EvidenceRetentionPeriod);
        }
        catch (Exception ex)
        {
            _ = cachedMessage;
            _logger.Warning(ex, "Failed to log message edit for {MessageId}.", updatedMessage.Id);
        }
    }

    private async Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        IMessageChannel? channelValue = channel.HasValue ? channel.Value : null;

        if (channelValue is null || !TryGetGuildId(channelValue, out ulong guildId))
            return;

        try
        {
            LoggingConfiguration configuration = await loggingStore.GetConfigurationAsync(guildId, CancellationToken.None).ConfigureAwait(false);

            if (!configuration.Enabled)
                return;

            bool isExcluded = await loggingStore.IsExcludedAsync(guildId, channelValue.Id, CancellationToken.None).ConfigureAwait(false);

            if (isExcluded)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            ObservedMessage? observed = await loggingStore.FindObservedMessageAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
            evidenceCache.TryGet(message.Id, now, out MessageEvidence evidence);

            IUserMessage? logMessage = await SendLogAsync(
                    configuration,
                    LoggingEventKind.Delete,
                    BuildDeleteText(message.Id, channelValue.Id, observed, evidence, now),
                    evidence.Attachments
                )
                .ConfigureAwait(false);

            if (logMessage is not null)
                await loggingStore
                    .RecordLogEntriesAsync(new[] { new MessageLogEntry(message.Id, logMessage.Id, now) }, CancellationToken.None)
                    .ConfigureAwait(false);

            evidenceCache.Remove(message.Id);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to log message deletion for {MessageId}.", message.Id);
        }
    }

    private async Task HandleMessagesBulkDeletedAsync(
        IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel
    )
    {
        IMessageChannel? channelValue = channel.HasValue ? channel.Value : null;

        if (channelValue is null || !TryGetGuildId(channelValue, out ulong guildId))
            return;

        try
        {
            LoggingConfiguration configuration = await loggingStore.GetConfigurationAsync(guildId, CancellationToken.None).ConfigureAwait(false);

            if (!configuration.Enabled)
                return;

            bool isExcluded = await loggingStore.IsExcludedAsync(guildId, channelValue.Id, CancellationToken.None).ConfigureAwait(false);

            if (isExcluded)
                return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            ImmutableArray<ulong> messageIds = messages.Select(message => message.Id).Distinct().ToImmutableArray();
            IReadOnlyDictionary<ulong, ObservedMessage> observed = await loggingStore
                .FindObservedMessagesAsync(messageIds, CancellationToken.None)
                .ConfigureAwait(false);
            IReadOnlyDictionary<ulong, MessageEvidence> evidence = evidenceCache.GetMany(messageIds, now);

            IUserMessage? logMessage = await SendLogAsync(
                    configuration,
                    LoggingEventKind.BulkDelete,
                    BuildBulkDeleteText(channelValue.Id, messageIds, observed, evidence, now),
                    FlattenAttachments(evidence.Values)
                )
                .ConfigureAwait(false);

            if (logMessage is not null)
            {
                MessageLogEntry[] entries = messageIds.Select(messageId => new MessageLogEntry(messageId, logMessage.Id, now)).ToArray();

                await loggingStore.RecordLogEntriesAsync(entries, CancellationToken.None).ConfigureAwait(false);
            }

            foreach (ulong messageId in messageIds)
                evidenceCache.Remove(messageId);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to log bulk message deletion in channel {ChannelId}.", channelValue.Id);
        }
    }

    private async Task<MessageEvidence> CreateEvidenceAsync(ulong guildId, SocketUserMessage message, DateTimeOffset now, CancellationToken ct)
    {
        ImmutableArray<CachedAttachmentEvidence>.Builder attachments = ImmutableArray.CreateBuilder<CachedAttachmentEvidence>();

        foreach (Attachment attachment in message.Attachments.Take(_options.MaxAttachmentCountPerMessage))
        {
            byte[]? bytes = await TryDownloadAttachmentAsync(attachment.Url, ct).ConfigureAwait(false);

            if (bytes is not null)
                attachments.Add(new CachedAttachmentEvidence(attachments.Count + 1, bytes, attachment.ContentType ?? "application/octet-stream"));
        }

        return new MessageEvidence(
            guildId,
            message.Channel.Id,
            message.Id,
            message.Author.Id,
            now,
            NullIfEmpty(message.Content),
            attachments.ToImmutable()
        );
    }

    private Task<byte[]?> TryDownloadAttachmentAsync(string url, CancellationToken ct) =>
        AttachmentEvidenceDownloader.TryDownloadAsync(httpClient, url, _options.MaxAttachmentBytesPerAttachment, _logger, ct);

    private async Task<IUserMessage?> SendLogAsync(
        LoggingConfiguration configuration,
        LoggingEventKind eventKind,
        string text,
        IReadOnlyCollection<CachedAttachmentEvidence> attachments
    )
    {
        ulong? destinationChannelId = configuration.GetDestinationChannelId(eventKind);

        if (destinationChannelId is null || discordClient.GetChannel(destinationChannelId.Value) is not IMessageChannel logChannel)
            return null;

        if (attachments.Count == 0)
            return await logChannel.SendMessageAsync(text).ConfigureAwait(false);

        Queue<FileAttachment> files = new Queue<FileAttachment>();
        Queue<MemoryStream> streams = new Queue<MemoryStream>();

        try
        {
            foreach (CachedAttachmentEvidence attachment in attachments)
            {
                MemoryStream stream = new MemoryStream(attachment.Bytes);
                streams.Enqueue(stream);
                files.Enqueue(new FileAttachment(stream, $"evidence-{attachment.Index}.bin"));
            }

            return await logChannel.SendFilesAsync(files, text).ConfigureAwait(false);
        }
        finally
        {
            foreach (MemoryStream stream in streams)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string BuildEditText(SocketUserMessage message, MessageEvidence before, MessageEvidence after)
    {
        string beforeContent = before.Content is null ? "unavailable" : CodeBlock(before.Content);
        string afterContent = after.Content is null ? "unavailable" : CodeBlock(after.Content);

        return $"Message edited in <#{message.Channel.Id}> by <@{message.Author.Id}> (`{message.Author.Id}`), message `{message.Id}`.\n"
            + $"Before: {beforeContent}\n"
            + $"After: {afterContent}";
    }

    private static string BuildDeleteText(
        ulong messageId,
        ulong channelId,
        ObservedMessage? observed,
        MessageEvidence evidence,
        DateTimeOffset deletedAtUtc
    )
    {
        string author =
            observed is not null ? $"<@{observed.AuthorId}> (`{observed.AuthorId}`)"
            : evidence.AuthorId == 0 ? "unknown"
            : $"<@{evidence.AuthorId}> (`{evidence.AuthorId}`)";
        string observedAt = observed is null ? "unknown" : observed.ObservedAtUtc.ToString("O");
        string content = evidence.Content is null ? "unavailable" : CodeBlock(evidence.Content);

        return $"Message deleted in <#{channelId}>. Author: {author}. Message: `{messageId}`. Observed: {observedAt}. Deleted: {deletedAtUtc:O}.\n"
            + $"Cached content: {content}";
    }

    private static string BuildBulkDeleteText(
        ulong channelId,
        ImmutableArray<ulong> messageIds,
        IReadOnlyDictionary<ulong, ObservedMessage> observed,
        IReadOnlyDictionary<ulong, MessageEvidence> evidence,
        DateTimeOffset deletedAtUtc
    )
    {
        int attributedCount = messageIds.Count(observed.ContainsKey);
        int evidenceCount = messageIds.Count(evidence.ContainsKey);
        string details = BuildBulkDeleteDetails(messageIds, observed, evidence);

        return $"Bulk delete in <#{channelId}> at {deletedAtUtc:O}. Messages: {messageIds.Length}. "
            + $"Attributed: {attributedCount}. Cached evidence: {evidenceCount}.\n"
            + details;
    }

    private static string BuildBulkDeleteDetails(
        ImmutableArray<ulong> messageIds,
        IReadOnlyDictionary<ulong, ObservedMessage> observed,
        IReadOnlyDictionary<ulong, MessageEvidence> evidence
    )
    {
        List<string> lines = new List<string>();

        foreach (ulong messageId in messageIds.Take(20))
        {
            string author = observed.TryGetValue(messageId, out ObservedMessage? observedMessage)
                ? $"author <@{observedMessage.AuthorId}> (`{observedMessage.AuthorId}`)"
                : "author unknown";
            string content =
                evidence.TryGetValue(messageId, out MessageEvidence? messageEvidence) && messageEvidence.Content is not null
                    ? $" content: {SingleLineSnippet(messageEvidence.Content)}"
                    : string.Empty;

            lines.Add($"- `{messageId}` {author}.{content}");
        }

        if (messageIds.Length > 20)
            lines.Add($"- and {messageIds.Length - 20} more message(s).");

        return string.Join('\n', lines);
    }

    private static ImmutableArray<CachedAttachmentEvidence> FlattenAttachments(IEnumerable<MessageEvidence> evidence) =>
        evidence.SelectMany(item => item.Attachments).Take(10).ToImmutableArray();

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string CodeBlock(string value)
    {
        string sanitized = value.Replace("```", "`\u200b``", StringComparison.Ordinal);
        return $"```{sanitized}```";
    }

    private static string SingleLineSnippet(string value)
    {
        string sanitized = value.ReplaceLineEndings(" ").Trim();

        if (sanitized.Length > 140)
            sanitized = $"{sanitized.AsSpan(0, 137)}...";

        return $"`{sanitized.Replace('`', '\'')}`";
    }

    private static bool TryGetGuildId(IChannel channel, out ulong guildId)
    {
        switch (channel)
        {
            case IGuildChannel guildChannel:
                guildId = guildChannel.GuildId;
                return true;
            default:
                guildId = 0;
                return false;
        }
    }

    private void Subscribe()
    {
        discordClient.MessageReceived += HandleMessageReceivedAsync;
        discordClient.MessageUpdated += HandleMessageUpdatedAsync;
        discordClient.MessageDeleted += HandleMessageDeletedAsync;
        discordClient.MessagesBulkDeleted += HandleMessagesBulkDeletedAsync;
    }
}
