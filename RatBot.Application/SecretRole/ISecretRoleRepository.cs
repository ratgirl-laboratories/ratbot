using RatBot.Domain.SecretRole;

namespace RatBot.Application.SecretRole;

public interface ISecretRoleRepository
{
    Task<IReadOnlyList<SecretRoleSetting>> ListAsync(CancellationToken ct);

    Task<SecretRoleSetting> ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct);
}
