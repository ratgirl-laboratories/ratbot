using RatBot.Application.Features.Quorum;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Configuration.Quorum;

public sealed class EfQuorumConfigurationRepository(BotDbContext dbContext) : IQuorumConfigurationRepository
{
    public async Task<QuorumConfig?> GetAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        CancellationToken ct = default)
    {
        QuorumConfigEntity? entity = await dbContext.Set<QuorumConfigEntity>()
            .AsNoTracking()
            .Include(config => config.Roles)
            .SingleOrDefaultAsync(
                config => config.GuildId == guildId && config.TargetType == targetType && config.TargetId == targetId,
                ct);

        return entity is null
            ? null
            : Map(entity);
    }

    public async Task<bool> UpsertAsync(QuorumConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        QuorumConfigEntity? entity = await dbContext.Set<QuorumConfigEntity>()
            .Include(existing => existing.Roles)
            .SingleOrDefaultAsync(
                existing => existing.GuildId == config.GuildId && existing.TargetType == config.TargetType &&
                            existing.TargetId == config.TargetId,
                ct);

        if (entity is null)
        {
            dbContext.Add(Map(config));
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        entity.QuorumProportion = config.QuorumProportion;
        dbContext.RemoveRange(entity.Roles);

        entity.Roles = config.RoleIds.Select(roleId => new QuorumConfigRoleEntity(roleId))
            .ToList();

        await dbContext.SaveChangesAsync(ct);
        return false;
    }

    public async Task<bool> DeleteAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        CancellationToken ct = default)
    {
        QuorumConfigEntity? entity = await dbContext.Set<QuorumConfigEntity>()
            .Include(existing => existing.Roles)
            .SingleOrDefaultAsync(
                existing => existing.GuildId == guildId && existing.TargetType == targetType &&
                            existing.TargetId == targetId,
                ct);

        if (entity is null)
            return false;

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static QuorumConfig Map(QuorumConfigEntity entity) =>
        new QuorumConfig(
            entity.GuildId,
            entity.TargetType,
            entity.TargetId,
            entity.Roles.Select(role => role.RoleId).ToArray(),
            entity.QuorumProportion);

    private static QuorumConfigEntity Map(QuorumConfig config) =>
        new QuorumConfigEntity
        {
            GuildId = config.GuildId,
            TargetType = config.TargetType,
            TargetId = config.TargetId,
            QuorumProportion = config.QuorumProportion,
            Roles = config.RoleIds.Select(roleId => new QuorumConfigRoleEntity(roleId)).ToList()
        };
}