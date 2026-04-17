using RatBot.Application.Features.Meta.Errors;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Domain.Primitives;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Settings.Meta;

public sealed class MetaSuggestionSettingsRepository(BotDbContext dbContext) : IMetaSuggestionSettingsRepository
{
    public async Task<ErrorOr<MetaSuggestionSettings>> GetSettingsAsync(GuildSnowflake guildId, CancellationToken ct = default)
    {
        MetaSuggestionSettings? settings = await dbContext
            .Set<MetaSuggestionSettings>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.GuildId == guildId, ct);

        return settings is null
            ? MetaSuggestionErrors.ForumNotConfigured
            : settings;
    }

    public async Task<ErrorOr<Success>> SaveSettingsAsync(
        MetaSuggestionSettings settings,
        CancellationToken ct = default)
    {
        bool exists = await dbContext
            .Set<MetaSuggestionSettings>()
            .AnyAsync(x => x.GuildId == settings.GuildId, ct);

        if (!exists)
        {
            dbContext.Add(settings);
        }
        else
        {
            dbContext.Update(settings);
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success;
    }
}
