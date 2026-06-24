using RatBot.Domain.SecretRole;

namespace RatBot.Application.SecretRole;

public interface ISecretRoleRepository
{
    Task<SecretRoleSetting?> GetAsync(CancellationToken ct);

    Task<SecretRoleSetting> ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct);
}
