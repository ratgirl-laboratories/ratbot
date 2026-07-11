using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application;
using RatBot.Discord.Features.Logging.Gateway;
using RatBot.Discord.Gateway;
using RatBot.Infrastructure;
using Serilog;
using Shouldly;

namespace RatBot.Discord.Tests;

public sealed class DependencyInjectionTests
{
    [Test]
    public async Task RealServiceGraph_HasNoCaptiveScopedDependencies()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["DB:ConnectionString"] = "Host=localhost;Database=ratbot_di_test;Username=ratbot;Password=unused",
                    ["Discord:Token"] = "inert-test-token",
                    ["Discord:GuildId"] = "1",
                    ["Discord:MessageCacheSize"] = "1000",
                    ["AdventureLeaderboard:AdventurerRoleId"] = "1",
                }
            )
            .Build();

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton<ILogger>(new LoggerConfiguration().CreateLogger());
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddDiscordAdapter(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );

        IDiscordGatewayHandler[] gatewayHandlers = provider.GetServices<IDiscordGatewayHandler>().ToArray();

        gatewayHandlers.ShouldContain(handler => handler is AutobanGatewayHandler);
        gatewayHandlers.ShouldContain(handler => handler is ModerationLoggingGatewayHandler);
        provider.GetRequiredService<AutobanGatewayHandler>().ShouldNotBeNull();
        provider.GetRequiredService<ModerationLoggingGatewayHandler>().ShouldNotBeNull();
    }
}
