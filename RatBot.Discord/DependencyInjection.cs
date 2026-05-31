using Microsoft.Extensions.Options;
using RatBot.Application.Common.Forums;
using RatBot.Application.Common.Interfaces;
using RatBot.Discord.BackgroundWorkers;
using RatBot.Discord.Commands.Emoji;
using RatBot.Discord.Forum;
using RatBot.Discord.Commands.Settings;
using RatBot.Discord.Configuration;
using RatBot.Discord.Gateway;
using RatBot.Discord.Handlers;
using RatBot.Discord.Hosting;

namespace RatBot.Discord;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddDiscordAdapter(IConfiguration configuration)
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
                        GatewayIntents =
                            GatewayIntents.Guilds
                            | GatewayIntents.GuildMembers
                            | GatewayIntents.GuildMessages
                            | GatewayIntents.GuildMessageReactions
                            | GatewayIntents.MessageContent,
                    }
                );
            });

            services.AddSingleton(sp => new InteractionService(
                sp.GetRequiredService<DiscordSocketClient>(),
                new InteractionServiceConfig { AutoServiceScopes = true }
            ));

            services.AddSingleton<DiscordInteractionHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<DiscordInteractionHandler>());
            services.AddSingleton<AutobanGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<AutobanGatewayHandler>());
            services.AddSingleton<ReactionGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<ReactionGatewayHandler>());
            services.AddSingleton<MessageContentGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<MessageContentGatewayHandler>());
            services.AddSingleton<TrialRunDetectionLogger>();
            services.AddSingleton<ImageBurstSpamGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<ImageBurstSpamGatewayHandler>());
            services.AddSingleton<UserUpdatedGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<UserUpdatedGatewayHandler>());
            services.AddSingleton<GuildMemberCacheService>();
            services.AddSingleton<ITrackedEmojiCatalog, TrackedEmojiCatalog>();
            services.AddSingleton<IQuorumCommandInputResolver, QuorumCommandInputResolver>();
            services.AddSingleton<IRoleColourReconciler, RoleColourReconciler>();


            // Role-colour sync queue and background worker
            services.AddSingleton<IRoleColourSyncQueue, RoleColourSyncQueue>();

            services.AddHostedService<DiscordBotHostedService>();
            services.AddHostedService<GuildMemberCacheBackgroundWorker>();
            services.AddHostedService<EmojiAnalyticsBackgroundWorker>();
            services.AddHostedService<RoleColourSyncBackgroundWorker>();

            services.AddSingleton<IForumThreadClient, ForumThreadClient>();
        }
    }
}
