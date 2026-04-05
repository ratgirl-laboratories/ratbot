using Discord.Interactions;
using RatBot.Discord;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Services;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

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
    public async static Task Main(string[] args)
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
                    string serviceInstanceId = ctx.Configuration["OTEL:Resource:ServiceInstanceId"] ?? Environment.MachineName;

                    loggerConfiguration
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .Enrich.WithProperty("service_name", ctx.Configuration["OTEL:Resource:ServiceName"] ?? "ratbot")
                        .Enrich.WithProperty("service_instance_id", serviceInstanceId)
                        .Enrich.WithProperty(
                            "environment",
                            ctx.Configuration["OTEL:Resource:Environment"] ?? ctx.Configuration["ASPNETCORE_ENVIRONMENT"] ?? "production"
                        )
                        .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                        .WriteTo.File("logs/verbose-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .WriteTo.File("logs/debug-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Debug)
                        .WriteTo.File("logs/info-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Information)
                        .WriteTo.File("logs/warning-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Warning)
                        .WriteTo.File("logs/error-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Error);

                    ConfigureOpenTelemetryLogs(ctx.Configuration, loggerConfiguration);
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

                    string connectionString = PostgresConnectionStringBuilder.Build(config);

                    services.AddDbContext<BotDbContext>(opt => opt.UseNpgsql(connectionString));

                    #endregion

                    #region Application Services

                    services.AddScoped<QuorumConfigService>();
                    services.AddScoped<EmojiUsageService>();

                    #endregion
                }
            )
            .Build();

        LogOpenTelemetryStartupConfiguration(host.Services.GetRequiredService<IConfiguration>());
        await ApplyDatabaseMigrationsAsync(host);
        Log.Information("OpenTelemetry logging pipeline startup test event.");

        await host.RunAsync();
    }

    private static async Task ApplyDatabaseMigrationsAsync(IHost host)
    {
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            BotDbContext dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

            // Apply any migrations that have not yet been applied to the current database.
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

    private static void ConfigureOpenTelemetryLogs(IConfiguration config, LoggerConfiguration loggerConfiguration)
    {
        string? endpoint = config["OTEL:Logs:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        string serviceName = config["OTEL:Resource:ServiceName"] ?? "ratbot";
        string serviceInstanceId = config["OTEL:Resource:ServiceInstanceId"] ?? Environment.MachineName;
        string environment = config["OTEL:Resource:Environment"] ?? config["ASPNETCORE_ENVIRONMENT"] ?? "production";
        string configuredProtocol = config["OTEL:Logs:Protocol"] ?? "grpc";
        OtlpProtocol protocol =
            configuredProtocol.Equals("http", StringComparison.OrdinalIgnoreCase)
            || configuredProtocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpProtocol.HttpProtobuf
                : OtlpProtocol.Grpc;

        loggerConfiguration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = endpoint;
            options.Protocol = protocol;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = serviceName,
                ["service.instance.id"] = serviceInstanceId,
                ["service_name"] = serviceName,
                ["service_instance_id"] = serviceInstanceId,
                ["environment"] = environment,
            };
        });
    }

    private static void EnableSerilogSelfDiagnostics()
    {
        SelfLog.Enable(message => Console.Error.WriteLine($"[SerilogSelfLog] {message}"));
    }

    private static void LogOpenTelemetryStartupConfiguration(IConfiguration config)
    {
        string? endpoint = config["OTEL:Logs:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Log.Warning("OpenTelemetry sink is disabled because OTEL:Logs:Endpoint is not configured.");
            return;
        }

        string serviceName = config["OTEL:Resource:ServiceName"] ?? "ratbot";
        string serviceInstanceId = config["OTEL:Resource:ServiceInstanceId"] ?? Environment.MachineName;
        string environment = config["OTEL:Resource:Environment"] ?? config["ASPNETCORE_ENVIRONMENT"] ?? "production";
        string protocol = config["OTEL:Logs:Protocol"] ?? "grpc";

        Log.Information(
            "OpenTelemetry sink configured. Endpoint={OtelEndpoint} Protocol={OtelProtocol} ServiceName={ServiceName} ServiceInstanceId={ServiceInstanceId} Environment={Environment}",
            endpoint,
            protocol,
            serviceName,
            serviceInstanceId,
            environment
        );
    }
}
