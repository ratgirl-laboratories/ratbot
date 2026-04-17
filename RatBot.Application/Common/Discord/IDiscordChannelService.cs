namespace RatBot.Application.Common.Discord;

public interface IDiscordChannelService
{
    Task<ErrorOr<ResolvedTextChannel>> GetTextChannelAsync(ulong channelId);
    Task<ErrorOr<Success>> ValidateBotCanSendAsync(ulong channelId);
    Task<ErrorOr<int>> SendMessagesAsync(ulong channelId, IReadOnlyList<string> messages);
}

public sealed record ResolvedTextChannel(ulong Id, string Mention);