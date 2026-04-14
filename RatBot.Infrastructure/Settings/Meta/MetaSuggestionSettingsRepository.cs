using RatBot.Application.Features.Meta.Errors;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Settings.Meta;

public sealed class MetaSuggestionSettingsRepository(BotDbContext dbContext) : IMetaSuggestionSettingsRepository
{
    public async Task<ErrorOr<ulong>> GetSuggestForumChannelIdAsync(ulong guildId, CancellationToken ct = default)
    {
        MetaSuggestionSettingsEntity? entity = await dbContext
            .Set<MetaSuggestionSettingsEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.GuildId == guildId, ct);

        return entity is null
            ? MetaSuggestionErrors.ForumNotConfigured
            : entity.SuggestForumChannelId;
    }

    public async Task<ErrorOr<bool>> UpsertSuggestForumChannelAsync(
        ulong guildId,
        ulong forumChannelId,
        CancellationToken ct = default)
    {
        MetaSuggestionSettingsEntity? existing = await dbContext
            .Set<MetaSuggestionSettingsEntity>()
            .SingleOrDefaultAsync(x => x.GuildId == guildId, ct);

        if (existing is null)
        {
            dbContext.Add(new MetaSuggestionSettingsEntity
            {
                GuildId = guildId,
                SuggestForumChannelId = forumChannelId
            });

            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        dbContext.Update(existing with { SuggestForumChannelId = forumChannelId });
        await dbContext.SaveChangesAsync(ct);
        return false;
    }
}
