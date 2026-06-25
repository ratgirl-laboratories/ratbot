using RatBot.Application.Modules.Quorum;
using RatBot.Domain.Modules.Quorum;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Persistence.Quorum;

public sealed class QuorumConfigurationStore(IDbContextFactory<BotDbContext> dbContexts) : IQuorumConfigurationStore
{
    public async Task<ErrorOr<QuorumConfiguration>> GetAsync(QuorumScope scope, CancellationToken ct)
    {
        await using BotDbContext db = await dbContexts.CreateDbContextAsync(ct).ConfigureAwait(false);

        QuorumConfigurationRow? row = await db.Set<QuorumConfigurationRow>()
            .AsNoTracking()
            .Include(configuration => configuration.VoterRoles)
            .SingleOrDefaultAsync(configuration => configuration.GuildId == scope.GuildId && configuration.ChannelId == scope.ChannelId, ct)
            .ConfigureAwait(false);

        return row is null ? QuorumErrors.ConfigurationNotFound : QuorumPersistenceMapping.ToDomain(row);
    }

    public async Task<ErrorOr<QuorumRegistration>> RegisterAsync(QuorumScope scope, QuorumProportion proportion, CancellationToken ct)
    {
        await using BotDbContext db = await dbContexts.CreateDbContextAsync(ct).ConfigureAwait(false);

        QuorumConfigurationRow? row = await db.Set<QuorumConfigurationRow>()
            .Include(configuration => configuration.VoterRoles)
            .SingleOrDefaultAsync(configuration => configuration.GuildId == scope.GuildId && configuration.ChannelId == scope.ChannelId, ct)
            .ConfigureAwait(false);

        bool created = row is null;

        if (row is null)
        {
            row = QuorumPersistenceMapping.NewRow(scope, proportion);
            db.Add(row);
        }
        else
        {
            row.Proportion = proportion.Value;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new QuorumRegistration(created, QuorumPersistenceMapping.ToDomain(row));
    }

    public async Task<ErrorOr<QuorumConfiguration>> SaveAsync(QuorumConfiguration configuration, CancellationToken ct)
    {
        await using BotDbContext db = await dbContexts.CreateDbContextAsync(ct).ConfigureAwait(false);

        QuorumConfigurationRow? row = await db.Set<QuorumConfigurationRow>()
            .Include(existing => existing.VoterRoles)
            .SingleOrDefaultAsync(existing => existing.Id == configuration.Id.Value, ct)
            .ConfigureAwait(false);

        if (row is null)
            return Error.NotFound(description: "Quorum configuration not found.");

        row.Proportion = configuration.Proportion.Value;

        db.RemoveRange(row.VoterRoles);
        row.VoterRoles.Clear();

        foreach (ulong roleId in configuration.VoterRoles.RoleIds.Order())
        {
            row.VoterRoles.Add(new QuorumVoterRoleRow { QuorumConfigurationId = row.Id, RoleId = roleId });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return QuorumPersistenceMapping.ToDomain(row);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(QuorumScope scope, CancellationToken ct)
    {
        await using BotDbContext db = await dbContexts.CreateDbContextAsync(ct).ConfigureAwait(false);

        int deleted = await db.Set<QuorumConfigurationRow>()
            .Where(configuration => configuration.GuildId == scope.GuildId && configuration.ChannelId == scope.ChannelId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        return deleted > 0 ? Result.Deleted : QuorumErrors.ConfigurationNotFound;
    }
}
