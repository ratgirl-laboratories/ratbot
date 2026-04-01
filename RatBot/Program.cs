using Discord.Commands;
using Discord.Interactions;
using RatBot.Discord;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Services;
using Serilog.Events;

namespace RatBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Env.TraversePath().Load();

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(
                (_, configurationBuilder) =>
                {
                    configurationBuilder.AddEnvironmentVariables();
                }
            )
            .UseSerilog(
                (_, _, loggerConfiguration) =>
                    loggerConfiguration
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
                        .WriteTo.File(
                            "logs/verbose-.log",
                            rollingInterval: RollingInterval.Day,
                            restrictedToMinimumLevel: LogEventLevel.Verbose
                        )
                        .WriteTo.File(
                            "logs/debug-.log",
                            rollingInterval: RollingInterval.Day,
                            restrictedToMinimumLevel: LogEventLevel.Debug
                        )
                        .WriteTo.File(
                            "logs/info-.log",
                            rollingInterval: RollingInterval.Day,
                            restrictedToMinimumLevel: LogEventLevel.Information
                        )
                        .WriteTo.File(
                            "logs/warning-.log",
                            rollingInterval: RollingInterval.Day,
                            restrictedToMinimumLevel: LogEventLevel.Warning
                        )
                        .WriteTo.File(
                            "logs/error-.log",
                            rollingInterval: RollingInterval.Day,
                            restrictedToMinimumLevel: LogEventLevel.Error
                        )
            )
            .ConfigureServices(
                (ctx, services) =>
                {
                    IConfiguration config = ctx.Configuration;

                    #region Discord Core Services

                    services.AddSingleton(_ => new DiscordSocketClient(
                        new DiscordSocketConfig
                        {
                            GatewayIntents =
                                GatewayIntents.Guilds
                                | GatewayIntents.GuildMembers
                                | GatewayIntents.GuildMessages
                                | GatewayIntents.GuildMessageReactions
                                | GatewayIntents.MessageContent,
                        }
                    ));

                    services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
                    services.AddSingleton<CommandService>();
                    services.AddSingleton<VirtueModule>();
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
                    services.AddScoped<EmojiVirtueService>();
                    services.AddScoped<EmojiUsageService>();
                    services.AddScoped<VirtueReactionLockService>();
                    services.AddScoped<VirtueRoleTierConfigService>();

                    #endregion
                }
            )
            .Build();

        await ApplyDatabaseMigrationsAsync(host);

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
                Console.WriteLine($"[DB] Applying {pending.Count} pending migration(s)...");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("[DB] Migrations applied successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Migration/creation failed: {ex}");
            throw;
        }
    }
}
