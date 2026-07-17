using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.SecretRole;
using RatBot.Discord.SecretRole;
using RatBot.Domain.SecretRole;
using Shouldly;

namespace RatBot.Discord.Tests.SecretRole;

[TestFixture]
public sealed class SecretRoleManagerTests
{
    [Test]
    public async Task InitializeAsync_CachesSettingsByGuild()
    {
        FakeSecretRoleRepository repository = new FakeSecretRoleRepository(
            new SecretRoleSetting { GuildId = 1, RoleId = 101 },
            new SecretRoleSetting { GuildId = 2, RoleId = 202 }
        );
        SecretRoleManager manager = CreateManager(repository);

        await manager.InitializeAsync(CancellationToken.None);

        manager.GetCurrent(1)!.RoleId.ShouldBe(101UL);
        manager.GetCurrent(2)!.RoleId.ShouldBe(202UL);
        manager.GetCurrent(3).ShouldBeNull();
    }

    [Test]
    public async Task ReplaceAsync_UpdatesOnlyTheTargetGuild()
    {
        FakeSecretRoleRepository repository = new FakeSecretRoleRepository(
            new SecretRoleSetting { GuildId = 1, RoleId = 101 },
            new SecretRoleSetting { GuildId = 2, RoleId = 202 }
        );
        SecretRoleManager manager = CreateManager(repository);
        await manager.InitializeAsync(CancellationToken.None);

        await manager.ReplaceAsync(1, 111, CancellationToken.None);

        manager.GetCurrent(1)!.RoleId.ShouldBe(111UL);
        manager.GetCurrent(2)!.RoleId.ShouldBe(202UL);
    }

    private static SecretRoleManager CreateManager(ISecretRoleRepository repository)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(repository);
        ServiceProvider provider = services.BuildServiceProvider();
        return new SecretRoleManager(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class FakeSecretRoleRepository(params SecretRoleSetting[] initialSettings) : ISecretRoleRepository
    {
        private readonly Dictionary<ulong, SecretRoleSetting> _settings = initialSettings.ToDictionary(setting => setting.GuildId);

        public Task<SecretRoleSetting?> GetAsync(ulong guildId, CancellationToken ct) => Task.FromResult(_settings.GetValueOrDefault(guildId));

        public Task<IReadOnlyList<SecretRoleSetting>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SecretRoleSetting>>(_settings.Values.ToArray());

        public Task<SecretRoleSetting> ReplaceAsync(ulong guildId, ulong roleId, CancellationToken ct)
        {
            SecretRoleSetting setting = new SecretRoleSetting { GuildId = guildId, RoleId = roleId };
            _settings[guildId] = setting;
            return Task.FromResult(setting);
        }
    }
}
