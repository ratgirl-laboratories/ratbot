using Discord.Interactions;
using RatBot.Application;
using RatBot.Host.Configuration;
using RatBot.Host.Discord;
using RatBot.Infrastructure;

namespace RatBot.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddHostServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordOptions>()
            .Bind(configuration.GetSection(DiscordOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Token), "Discord token is required.")
            .Validate(options => options.GuildId != 0, "Discord guild id is required.")
            .Validate(options => options.MessageCacheSize >= 1000, "Discord message cache size must be at least 1000.")
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            DiscordOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscordOptions>>().Value;

            return new DiscordSocketClient(new DiscordSocketConfig
            {
                MessageCacheSize = options.MessageCacheSize,
                GatewayIntents = GatewayIntents.Guilds
                                 | GatewayIntents.GuildMembers
                                 | GatewayIntents.GuildMessages
                                 | GatewayIntents.GuildMessageReactions
                                 | GatewayIntents.MessageContent
            });
        });

        services.AddSingleton(sp =>
            new InteractionService(
                sp.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig { AutoServiceScopes = true }));

        services.AddSingleton<DiscordInteractionHandler>();
        services.AddSingleton<EmojiReactionGatewayHandler>();
        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<DiscordBotHostedService>();
        services.AddHostedService<EmojiAnalyticsBackgroundWorker>();

        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services;
    }
}
