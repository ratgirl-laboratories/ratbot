namespace RatBot.Domain.Features.Logging;

public sealed class MessageLogEntry
{
    private MessageLogEntry() { }

    public MessageLogEntry(ulong originalMessageId, ulong logMessageId, DateTimeOffset capturedAtUtc)
    {
        if (originalMessageId == 0)
            throw new ArgumentOutOfRangeException(nameof(originalMessageId), "Original message id is required.");

        if (logMessageId == 0)
            throw new ArgumentOutOfRangeException(nameof(logMessageId), "Log message id is required.");

        OriginalMessageId = originalMessageId;
        LogMessageId = logMessageId;
        CapturedAtUtc = capturedAtUtc;
    }

    public ulong OriginalMessageId { get; private set; }
    public ulong LogMessageId { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
}
