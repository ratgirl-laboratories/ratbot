namespace RatBot.Application.Administration;

public interface IMessageChannelWriter
{
    Task<ErrorOr<ResolvedMessageChannel>> GetChannelAsync(ulong channelId);
    Task<ErrorOr<int>> SendMessagesAsync(ulong channelId, IReadOnlyList<string> messages);
    Task<ErrorOr<Success>> ValidateBotCanSendAsync(ulong channelId);
}

public sealed record ResolvedMessageChannel(ulong Id, string Mention);
