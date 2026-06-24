using RatBot.Application.SecretRole;
using RatBot.Domain.SecretRole;

namespace RatBot.Discord.SecretRole;

public sealed class SecretRoleManager(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);
    private SecretRoleSetting? _current;

    public SecretRoleSetting? Current => Volatile.Read(ref _current);

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _updateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_current is not null)
                return;

            using IServiceScope scope = scopeFactory.CreateScope();
            ISecretRoleRepository store = scope.ServiceProvider.GetRequiredService<ISecretRoleRepository>();
            SecretRoleSetting? setting = await store.GetAsync(ct).ConfigureAwait(false);

            Volatile.Write(ref _current, setting);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct = default)
    {
        await _updateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ISecretRoleRepository store = scope.ServiceProvider.GetRequiredService<ISecretRoleRepository>();
            SecretRoleSetting setting = await store.ReplaceAsync(guildId, roleId, ct).ConfigureAwait(false);

            Volatile.Write(ref _current, setting);
        }
        finally
        {
            _updateLock.Release();
        }
    }
}
