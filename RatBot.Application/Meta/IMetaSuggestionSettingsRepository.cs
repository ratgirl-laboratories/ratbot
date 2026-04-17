namespace RatBot.Application.Meta;

public interface IMetaSuggestionSettingsRepository
{
    Task<ErrorOr<MetaSuggestionSettings>> GetSettingsAsync(ulong guildId, CancellationToken ct = default);

    Task<ErrorOr<Success>> SaveSettingsAsync(
        MetaSuggestionSettings settings,
        CancellationToken ct = default);
}