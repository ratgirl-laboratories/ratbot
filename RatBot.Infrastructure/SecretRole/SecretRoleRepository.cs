using RatBot.Application.SecretRole;
using RatBot.Domain.SecretRole;

namespace RatBot.Infrastructure.SecretRole;

public sealed class SecretRoleRepository(BotDbContext dbContext) : ISecretRoleRepository
{
    public Task<SecretRoleSetting?> GetAsync(ulong guildId, CancellationToken ct) =>
        dbContext.TemporaryPingRoleSettings.AsNoTracking().SingleOrDefaultAsync(x => x.GuildId == guildId, ct);

    public async Task<IReadOnlyList<SecretRoleSetting>> ListAsync(CancellationToken ct) =>
        await dbContext.TemporaryPingRoleSettings.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

    public async Task<SecretRoleSetting> ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct)
    {
        SecretRoleSetting? setting = await dbContext
            .TemporaryPingRoleSettings.SingleOrDefaultAsync(x => x.GuildId == guildId, ct)
            .ConfigureAwait(false);

        if (setting is null)
        {
            setting = new SecretRoleSetting { GuildId = guildId, RoleId = roleId };
            dbContext.TemporaryPingRoleSettings.Add(setting);
        }
        else
        {
            setting.RoleId = roleId;
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return setting;
    }
}
