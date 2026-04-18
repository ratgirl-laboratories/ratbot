using RatBot.Application.Quorum;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Persistence.Repositories;

public sealed class QuorumSettingsRepository(BotDbContext dbContext) : IQuorumSettingsRepository
{
    public async Task<ErrorOr<QuorumSettings>> GetAsync(ulong guildId, QuorumSettingsType targetType, ulong targetId)
    {
        QuorumSettings? config = await dbContext
            .Set<QuorumSettings>()
            .Include(config => config.Roles)
            .AsNoTracking()
            .SingleOrDefaultAsync(config =>
                config.GuildId == guildId && config.TargetType == targetType && config.TargetId == targetId);

        if (config is null)
            return Error.NotFound(description: "Quorum settings not found");

        return config;
    }

    public async Task<ErrorOr<Success>> UpsertAsync(QuorumSettings config)
    {
        bool exists = await dbContext
            .Set<QuorumSettings>()
            .AnyAsync(existing =>
                existing.GuildId == config.GuildId
                && existing.TargetType == config.TargetType
                && existing.TargetId == config.TargetId
            );

        if (!exists)
        {
            dbContext.Add(config);
        }
        else
        {
            await dbContext
                .Set<QuorumSettings>()
                .Where(existing =>
                    existing.GuildId == config.GuildId
                    && existing.TargetType == config.TargetType
                    && existing.TargetId == config.TargetId
                )
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.QuorumProportion, config.QuorumProportion));

            await dbContext
                .Set<QuorumSettingsRole>()
                .Where(role =>
                    role.GuildId == config.GuildId
                    && role.TargetType == config.TargetType
                    && role.TargetId == config.TargetId)
                .ExecuteDeleteAsync();

            dbContext.AddRange(config.Roles);
        }

        await dbContext.SaveChangesAsync();
        return Result.Success;
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(ulong guildId, QuorumSettingsType targetType, ulong targetId)
    {
        QuorumSettings? entity = await dbContext
            .Set<QuorumSettings>()
            .SingleOrDefaultAsync(existing =>
                existing.GuildId == guildId && existing.TargetType == targetType && existing.TargetId == targetId);

        if (entity is null)
            return Error.NotFound(description: "Quorum settings not found");

        await dbContext
            .Set<QuorumSettingsRole>()
            .Where(role => role.GuildId == guildId && role.TargetType == targetType && role.TargetId == targetId)
            .ExecuteDeleteAsync();

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync();
        return Result.Deleted;
    }
}