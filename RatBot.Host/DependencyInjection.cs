using Discord.Interactions;
using Microsoft.Extensions.Options;
using RatBot.Application;
using RatBot.Host.Configuration;
using RatBot.Host.Discord;
using RatBot.Infrastructure;
using RatBot.Interactions.Modules.Meta.Services;
using RatBot.Interactions.Modules.Meta.State;

namespace RatBot.Host;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddHostServices(IConfiguration configuration)
        {
            services
                .AddOptions<DiscordOptions>()
                .Bind(configuration.GetSection(DiscordOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Token), "Discord token is required.")
                .Validate(options => options.GuildId != 0, "Discord guild id is required.")
                .Validate(
                    options => options.MessageCacheSize >= 1000,
                    "Discord message cache size must be at least 1000.")
                .ValidateOnStart();

            services.AddSingleton(sp =>
            {
                DiscordOptions options = sp.GetRequiredService<IOptions<DiscordOptions>>().Value;

                return new DiscordSocketClient(
                    new DiscordSocketConfig
                    {
                        MessageCacheSize = options.MessageCacheSize,
                        GatewayIntents = GatewayIntents.Guilds
                                         | GatewayIntents.GuildMembers
                                         | GatewayIntents.GuildMessages
                                         | GatewayIntents.GuildMessageReactions
                                         | GatewayIntents.MessageContent
                    });
            });

            services.AddSingleton(sp => new InteractionService(
                sp.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig { AutoServiceScopes = true }));

            services.AddSingleton<DiscordInteractionHandler>();
            services.AddSingleton<EmojiReactionGatewayHandler>();
            services.AddSingleton<MetaSuggestionPendingStore>();
            services.AddSingleton<DiscordMetaSuggestionForumServiceFactory>(
                _ => guild => new DiscordMetaSuggestionForumService(guild));
            services.AddHostedService<DatabaseMigrationHostedService>();
            services.AddHostedService<DiscordBotHostedService>();
            services.AddHostedService<EmojiAnalyticsBackgroundWorker>();

            services.AddApplication();
            services.AddInfrastructure(configuration);
        }
    }
}
