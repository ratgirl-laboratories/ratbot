using Microsoft.EntityFrameworkCore;
using RatBot.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace RatBot.Infrastructure.Tests.Integration;

[SetUpFixture]
public sealed class PostgresDatabaseFixture
{
    private static PostgreSqlContainer _container = null!;

    public static string ConnectionString => _container.GetConnectionString();

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

    public static BotDbContext CreateDbContext()
    {
        DbContextOptions<BotDbContext> options = new DbContextOptionsBuilder<BotDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
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
}