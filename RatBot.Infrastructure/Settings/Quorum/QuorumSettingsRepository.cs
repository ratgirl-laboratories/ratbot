using Microsoft.EntityFrameworkCore.ChangeTracking;
using RatBot.Application.Features.Quorum;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Settings.Quorum;

public sealed class QuorumSettingsRepository(BotDbContext dbContext) : IQuorumSettingsRepository
{
    public async Task<ErrorOr<QuorumSettings>> GetAsync(ulong guildId, QuorumSettingsType targetType, ulong targetId)
    {
        QuorumSettings? config = await dbContext
            .Set<QuorumSettings>()
            .AsNoTracking()
            .SingleOrDefaultAsync(config =>
                config.GuildId == guildId && config.TargetType == targetType && config.TargetId == targetId);

        if (config is null)
            return Error.NotFound(description: "Quorum settings not found");

        List<ulong> roleIds = await dbContext
            .Set<QuorumSettingsRole>()
            .AsNoTracking()
            .Where(role =>
                EF.Property<ulong>(role, "GuildId") == guildId
                && EF.Property<QuorumSettingsType>(role, "TargetType") == targetType
                && EF.Property<ulong>(role, "TargetId") == targetId)
            .Select(role => role.Id)
            .ToListAsync();

        return config.Reconfigure(roleIds.ToArray(), config.QuorumProportion);
    }

    public async Task<ErrorOr<Success>> UpsertAsync(QuorumSettings config)
    {
        bool exists = await dbContext
            .Set<QuorumSettings>()
            .AnyAsync(existing =>
                existing.GuildId == config.GuildId
                && existing.TargetType == config.TargetType
                && existing.TargetId == config.TargetId);

        if (!exists)
            dbContext.Add(config);
        else
        {
            await dbContext
                .Set<QuorumSettings>()
                .Where(existing =>
                    existing.GuildId == config.GuildId
                    && existing.TargetType == config.TargetType
                    && existing.TargetId == config.TargetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.QuorumProportion, config.QuorumProportion));
        }

        await dbContext
            .Set<QuorumSettingsRole>()
            .Where(role =>
                EF.Property<ulong>(role, "GuildId") == config.GuildId
                && EF.Property<QuorumSettingsType>(role, "TargetType") == config.TargetType
                && EF.Property<ulong>(role, "TargetId") == config.TargetId)
            .ExecuteDeleteAsync();

        List<QuorumSettingsRole> roleRows = config.RoleIds.Select(roleId => new QuorumSettingsRole(roleId)).ToList();
        dbContext.AddRange(roleRows);

        foreach (EntityEntry<QuorumSettingsRole> entry in roleRows.Select(dbContext.Entry))
        {
            entry.Property("GuildId").CurrentValue = config.GuildId;
            entry.Property("TargetType").CurrentValue = config.TargetType;
            entry.Property("TargetId").CurrentValue = config.TargetId;
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
            .Where(role =>
                EF.Property<ulong>(role, "GuildId") == guildId
                && EF.Property<QuorumSettingsType>(role, "TargetType") == targetType
                && EF.Property<ulong>(role, "TargetId") == targetId)
            .ExecuteDeleteAsync();

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync();
        return Result.Deleted;
    }
}
