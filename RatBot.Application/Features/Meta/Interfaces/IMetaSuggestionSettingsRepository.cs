using RatBot.Domain.Primitives;

namespace RatBot.Application.Features.Meta.Interfaces;

public interface IMetaSuggestionSettingsRepository
{
    Task<ErrorOr<MetaSuggestionSettings>> GetSettingsAsync(GuildSnowflake guildId, CancellationToken ct = default);

    Task<ErrorOr<Success>> SaveSettingsAsync(
        MetaSuggestionSettings settings,
        CancellationToken ct = default);
}