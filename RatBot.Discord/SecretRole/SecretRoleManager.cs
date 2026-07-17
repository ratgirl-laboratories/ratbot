using RatBot.Application.SecretRole;
using RatBot.Domain.SecretRole;

namespace RatBot.Discord.SecretRole;

public sealed class SecretRoleManager(IServiceScopeFactory scopeFactory)
{
    private ImmutableDictionary<ulong, SecretRoleSetting> _current = ImmutableDictionary<ulong, SecretRoleSetting>.Empty;
    private readonly SemaphoreSlim _updateLock = new SemaphoreSlim(1, 1);

    public SecretRoleSetting? GetCurrent(ulong guildId)
    {
        ImmutableDictionary<ulong, SecretRoleSetting> snapshot = Volatile.Read(ref _current);
        return snapshot.GetValueOrDefault(guildId);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _updateLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (!_current.IsEmpty)
                return;

            using IServiceScope scope = scopeFactory.CreateScope();
            ISecretRoleRepository store = scope.ServiceProvider.GetRequiredService<ISecretRoleRepository>();
            IReadOnlyList<SecretRoleSetting> settings = await store.ListAsync(ct).ConfigureAwait(false);

            Volatile.Write(ref _current, settings.ToImmutableDictionary(setting => setting.GuildId));
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

            ImmutableDictionary<ulong, SecretRoleSetting> snapshot = Volatile.Read(ref _current);
            Volatile.Write(ref _current, snapshot.SetItem(guildId, setting));
        }
        finally
        {
            _updateLock.Release();
        }
    }
}
