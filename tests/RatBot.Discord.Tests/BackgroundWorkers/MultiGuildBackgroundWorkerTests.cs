using RatBot.Discord.BackgroundWorkers;
using Serilog;
using Shouldly;

namespace RatBot.Discord.Tests.BackgroundWorkers;

[TestFixture]
public sealed class MultiGuildBackgroundWorkerTests
{
    [Test]
    public async Task GuildMemberCacheWorker_VisitsEveryGuildAndContinuesAfterFailure()
    {
        List<ulong> visited = [];

        await GuildMemberCacheBackgroundWorker.ProcessGuildsAsync(
            [1UL, 2UL, 3UL],
            guildId => guildId,
            guildId =>
            {
                visited.Add(guildId);
                if (guildId == 2)
                    throw new InvalidOperationException("boom");
                return Task.CompletedTask;
            },
            Log.Logger,
            CancellationToken.None
        );

        visited.ShouldBe([1UL, 2UL, 3UL]);
    }

    [Test]
    public async Task RoleColourReconciliationWorker_VisitsAvailableConfiguredGuildsAndContinuesAfterFailure()
    {
        List<ulong> attempted = [];

        await RoleColourReconciliationBackgroundWorker.ProcessConfiguredGuildsAsync(
            [1UL, 2UL, 3UL, 4UL],
            guildId => guildId != 3,
            guildId =>
            {
                attempted.Add(guildId);
                if (guildId == 2)
                    throw new InvalidOperationException("boom");
                return Task.FromResult(0);
            },
            Log.Logger,
            CancellationToken.None
        );

        attempted.ShouldBe([1UL, 2UL, 4UL]);
    }
}
