using RatBot.Application.SecretRole;
using RatBot.Domain.SecretRole;

namespace RatBot.Infrastructure.SecretRole;

public sealed class SecretRoleRepository(BotDbContext dbContext) : ISecretRoleRepository
{
    public Task<SecretRoleSetting?> GetAsync(CancellationToken ct) =>
        dbContext.TemporaryPingRoleSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == SecretRoleSetting.SingletonId, ct);

    public async Task<SecretRoleSetting> ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct)
    {
        SecretRoleSetting? setting = await dbContext
            .TemporaryPingRoleSettings.SingleOrDefaultAsync(x => x.Id == SecretRoleSetting.SingletonId, ct)
            .ConfigureAwait(false);

        if (setting is null)
        {
            setting = new SecretRoleSetting { GuildId = guildId, RoleId = roleId };
            dbContext.TemporaryPingRoleSettings.Add(setting);
        }
        else
        {
            setting.GuildId = guildId;
            setting.RoleId = roleId;
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return setting;
    }
}
