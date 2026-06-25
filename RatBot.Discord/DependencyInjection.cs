using Microsoft.Extensions.Options;
using RatBot.Application.Common.Forums;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Features.Quorum;
using RatBot.Discord.BackgroundWorkers;
using RatBot.Discord.Commands.AdventureLeaderboard;
using RatBot.Discord.Commands.Emoji;
using RatBot.Discord.Commands.Meta;
using RatBot.Discord.Commands.Settings;
using RatBot.Discord.Configuration;
using RatBot.Discord.Features.Quorum;
using RatBot.Discord.Forum;
using RatBot.Discord.Gateway;
using RatBot.Discord.Handlers;
using RatBot.Discord.Hosting;
using RatBot.Discord.SecretRole;

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
                .Validate(options => options.MessageCacheSize >= 1000, "Discord message cache size must be at least 1000.")
                .ValidateOnStart();

            services
                .AddOptions<AdventureLeaderboardOptions>()
                .Bind(configuration.GetSection(AdventureLeaderboardOptions.SectionName))
                .Validate(options => options.AdventurerRoleId != 0, "Adventure role id is required.")
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
                            | GatewayIntents.GuildMessagePolls
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
            services.AddSingleton<ImageBurstSpamGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<ImageBurstSpamGatewayHandler>());
            services.AddSingleton<UserUpdatedGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<UserUpdatedGatewayHandler>());
            services.AddSingleton<MetaProposalPollResolver>();
            services.AddSingleton<MetaProposalGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<MetaProposalGatewayHandler>());
            services.AddSingleton<SecretRoleManager>();
            services.AddSingleton<SecretRoleGatewayHandler>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<SecretRoleGatewayHandler>());
            services.AddSingleton<GuildMemberCacheService>();
            services.AddSingleton<ITrackedEmojiCatalog, TrackedEmojiCatalog>();
            services.AddSingleton<AdventureLeaderboardClient>();
            services.AddSingleton<AdventureLeaderboardComponentBuilder>();
            services.AddSingleton<AdventureAccessController>();
            services.AddSingleton<AdventureLeaderboardManager>();
            services.AddSingleton<IQuorumCommandInputResolver, QuorumCommandInputResolver>();
            services.AddSingleton<IRoleColourReconciler, RoleColourReconciler>();

            // Role-colour sync queue and background worker
            services.AddSingleton<IRoleColourSyncQueue, RoleColourSyncQueue>();

            services.AddHostedService<DiscordBotHostedService>();
            services.AddHostedService<GuildMemberCacheBackgroundWorker>();
            services.AddHostedService<EmojiAnalyticsBackgroundWorker>();
            services.AddHostedService<RoleColourSyncBackgroundWorker>();
            services.AddHostedService<MetaProposalPollBackgroundWorker>();
            services.AddHostedService(sp => sp.GetRequiredService<AdventureLeaderboardManager>());

            // Quorum Module
            services.AddSingleton<DiscordQuorumMemberIndex>();
            services.AddSingleton<IQuorumMemberSource, DiscordQuorumMemberSource>();
            services.AddSingleton<IDiscordGatewayHandler>(sp => sp.GetRequiredService<DiscordQuorumMemberIndex>());

            services.AddSingleton<IForumThreadClient, ForumThreadClient>();
            services.AddSingleton<MetaProposalDiscordWorkflow>();
        }
    }
}
