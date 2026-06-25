using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RatBot.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace RatBot.Infrastructure.Tests.Integration;

[SetUpFixture]
[SuppressMessage("Structure", "NUnit1028:The non-test method is public")]
public sealed class PostgresDatabaseFixture
{
    private static PostgreSqlContainer _container = null!;

    private static string ConnectionString => _container.GetConnectionString();

    public static BotDbContext CreateDbContext()
    {
        DbContextOptions<BotDbContext> options = new DbContextOptionsBuilder<BotDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new BotDbContext(options);
    }

    public static async Task ResetAsync()
    {
        BotDbContext db = CreateDbContext();

        await using (db.ConfigureAwait(false))
        {
            await db.MemberColourPreferences.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.RoleColourOptions.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.QuorumSettingsRoles.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.QuorumSettings.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.MetaProposalStates.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.MetaSuggestionSettings.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.AutobannedUsers.ExecuteDeleteAsync().ConfigureAwait(false);
            await db.EmojiUsageCounts.ExecuteDeleteAsync().ConfigureAwait(false);
        }
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

        await _container.StartAsync();

        await using BotDbContext db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _container.DisposeAsync();
    }
}
