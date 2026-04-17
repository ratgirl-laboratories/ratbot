namespace RatBot.Application.Administration;

public interface IMessageChannelWriter
{
    Task<ErrorOr<ResolvedMessageChannel>> GetChannelAsync(ulong channelId);
    Task<ErrorOr<Success>> ValidateBotCanSendAsync(ulong channelId);
    Task<ErrorOr<int>> SendMessagesAsync(ulong channelId, IReadOnlyList<string> messages);
}

public sealed record ResolvedMessageChannel(ulong Id, string Mention);