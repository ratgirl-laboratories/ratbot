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

    public static string ConnectionString => _container.GetConnectionString();

    public static BotDbContext CreateDbContext()
    {
        DbContextOptions<BotDbContext> options = new DbContextOptionsBuilder<BotDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new BotDbContext(options);
    }

    public async static Task ResetAsync()
    {
        await using BotDbContext db = CreateDbContext();

        await db.QuorumSettingsRoles.ExecuteDeleteAsync();
        await db.QuorumSettings.ExecuteDeleteAsync();
        await db.MetaSuggestions.ExecuteDeleteAsync();
        await db.MetaSuggestionSettings.ExecuteDeleteAsync();
        await db.AutobannedUsers.ExecuteDeleteAsync();
        await db.EmojiUsageCounts.ExecuteDeleteAsync();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

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