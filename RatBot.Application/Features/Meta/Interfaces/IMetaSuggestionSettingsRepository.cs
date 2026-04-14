namespace RatBot.Application.Features.Meta.Interfaces;

public interface IMetaSuggestionSettingsRepository
{
    Task<ErrorOr<ulong>> GetSuggestForumChannelIdAsync(ulong guildId, CancellationToken ct = default);

    Task<ErrorOr<bool>> UpsertSuggestForumChannelAsync(
        ulong guildId,
        ulong forumChannelId,
        CancellationToken ct = default);
}
