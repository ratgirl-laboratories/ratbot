using RatBot.Application.Common;

namespace RatBot.Application.Administration;

public sealed class AdminSendService
{
    private readonly Func<string, ErrorOr<string[]>> _splitMessage;

    public AdminSendService()
        : this(message => MessageChunker.SplitMessageIntoChunks(message))
    {
    }

    internal AdminSendService(Func<string, ErrorOr<string[]>> splitMessage)
    {
        _splitMessage = splitMessage;
    }

    public async Task<ErrorOr<string>> SendAsync(IMessageChannelWriter channelWriter, ulong channelId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return AdminSendErrors.EmptyMessage;

        ErrorOr<ResolvedMessageChannel> channelResult = await channelWriter.GetChannelAsync(channelId);

        if (channelResult.IsError)
            return channelResult.Errors;

        ErrorOr<Success> permissionsResult = await channelWriter.ValidateBotCanSendAsync(channelId);

        if (permissionsResult.IsError)
            return permissionsResult.Errors;

        ErrorOr<string[]> chunksResult = _splitMessage(message);

        if (chunksResult.IsError)
            return chunksResult.Errors;

        string[] chunks = chunksResult.Value;
        ErrorOr<int> sendResult = await channelWriter.SendMessagesAsync(channelId, chunks);

        if (sendResult.IsError)
            return sendResult.Errors;

        int sentCount = sendResult.Value;
        ResolvedMessageChannel channel = channelResult.Value;

        return sentCount == 1
            ? $"Sent your message to {channel.Mention}."
            : $"Sent your message to {channel.Mention} in {sentCount} parts.";
    }
}