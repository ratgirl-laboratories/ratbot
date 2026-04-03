using Discord.Interactions;
using RatBot.Discord;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Services;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace RatBot;

/// <summary>
/// Application bootstrap entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Creates and runs the RatBot host.
    /// </summary>
    /// <param name="args">The process command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        Env.TraversePath().Load();
        EnableSerilogSelfDiagnostics();

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    configurationBuilder.AddEnvironmentVariables();
                }
            )
            .UseSerilog(
                (ctx, _, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("Application", "RatBot")
                        .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                        .WriteTo.File("logs/verbose-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .WriteTo.File("logs/debug-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
                        .WriteTo.File("logs/info-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Information)
                        .WriteTo.File("logs/warning-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Warning)
                        .WriteTo.File("logs/error-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Error);

                    ConfigureGrafanaLoki(ctx.Configuration, loggerConfiguration);
                }
            )
            .ConfigureServices(
                (ctx, services) =>
                {
                    IConfiguration config = ctx.Configuration;
                    int messageCacheSize = int.TryParse(config["Discord:MessageCacheSize"], out int configuredCacheSize)
                        ? Math.Max(configuredCacheSize, 1000)
                        : 5000;

                    #region Discord Core Services

                    services.AddSingleton(_ => new DiscordSocketClient(
                        new DiscordSocketConfig
                        {
                            MessageCacheSize = messageCacheSize,
                            GatewayIntents =
                                GatewayIntents.Guilds
                                | GatewayIntents.GuildMembers
                                | GatewayIntents.GuildMessages
                                | GatewayIntents.GuildMessageReactions
                                | GatewayIntents.MessageContent,
                        }
                    ));

                    services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
                    services.AddSingleton<DiscordBotService>();
                    services.AddHostedService<DiscordBotHostedService>();

                    #endregion

                    #region EF Core Services

                    string connectionString = MySqlConnectionStringBuilder.Build(config);

                    services.AddDbContext<BotDbContext>(opt => opt.UseMySQL(connectionString));

                    #endregion

                    #region Application Services

                    services.AddScoped<GuildConfigService>();
                    services.AddScoped<QuorumConfigService>();
                    services.AddScoped<UserVirtueService>();
                    services.AddScoped<EmojiUsageService>();

                    #endregion
                }
            )
            .Build();

        LogGrafanaLokiStartupConfiguration(host.Services.GetRequiredService<IConfiguration>());
        await ApplyDatabaseMigrationsAsync(host);
        Log.Information("Grafana Loki startup test event.");

        await host.RunAsync();
    }

    private static async Task ApplyDatabaseMigrationsAsync(IHost host)
    {
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            BotDbContext dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            // If there are migrations, apply pending ones; otherwise ensure database is created (first run)
            List<string> pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count != 0)
            {
                Log.Information("Applying {PendingMigrationCount} pending database migration(s).", pending.Count);
                await dbContext.Database.MigrateAsync();
                Log.Information("Database migrations applied successfully.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration/creation failed.");
            throw;
        }
    }

    private static void ConfigureGrafanaLoki(IConfiguration config, LoggerConfiguration loggerConfiguration)
    {
        string? uri = config["Grafana:Logs:Uri"];
        if (string.IsNullOrWhiteSpace(uri))
            return;

        string? username = config["Grafana:Logs:Username"];
        string? password = config["Grafana:Logs:Password"];
        string? environment = config["Grafana:Logs:Environment"] ?? config["ASPNETCORE_ENVIRONMENT"] ?? "production";
        string? host = Environment.MachineName;

        LokiCredentials? credentials = null;
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            credentials = new LokiCredentials { Login = username, Password = password };
        }

        loggerConfiguration.WriteTo.GrafanaLoki(
            uri,
            labels:
            [
                new LokiLabel { Key = "app", Value = "ratbot" },
                new LokiLabel { Key = "environment", Value = environment },
                new LokiLabel { Key = "host", Value = host },
            ],
            credentials: credentials,
            restrictedToMinimumLevel: LogEventLevel.Information
        );
    }

    private static void EnableSerilogSelfDiagnostics()
    {
        SelfLog.Enable(message => Console.Error.WriteLine($"[SerilogSelfLog] {message}"));
    }

    private static void LogGrafanaLokiStartupConfiguration(IConfiguration config)
    {
        string? uri = config["Grafana:Logs:Uri"];
        if (string.IsNullOrWhiteSpace(uri))
        {
            Log.Warning("Grafana Loki sink is disabled because Grafana:Logs:Uri is not configured.");
            return;
        }

        string? username = config["Grafana:Logs:Username"];
        string? password = config["Grafana:Logs:Password"];
        string? environment = config["Grafana:Logs:Environment"] ?? config["ASPNETCORE_ENVIRONMENT"] ?? "production";

        Log.Information(
            "Grafana Loki sink configured. Uri={LokiUri} Username={LokiUsername} HasPassword={HasPassword} Environment={Environment}",
            uri,
            username,
            !string.IsNullOrWhiteSpace(password),
            environment
        );
    }
}
