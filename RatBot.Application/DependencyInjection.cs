using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Administration;
using RatBot.Application.MessageContent;
using RatBot.Application.Meta;
using RatBot.Application.Moderation;
using RatBot.Application.Quorum;
using RatBot.Application.Reactions;
using RatBot.Application.Rps;

namespace RatBot.Application;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddApplication()
        {
            services.AddSingleton<ReactionQueue>();
            services.AddSingleton<MessageContentQueue>();
            services.AddSingleton(new ImageBurstSpamDetectorOptionsStore(new ImageBurstSpamDetectorOptions()));
            services.AddSingleton<ImageBurstSpamDetector>();

            services.AddScoped<AdminSendService>();
            services.AddScoped<ReactionUsageTracker>();
            services.AddScoped<EmojiUsageTracker>();
            services.AddScoped<MetaSuggestionService>();
            services.AddScoped<MetaSuggestionSettingsService>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddScoped<IQuorumSettingsReader, QuorumSettingsReader>();
            services.AddScoped<IQuorumSettingsWriter, QuorumSettingsWriter>();
            services.AddScoped<RpsGameService>();
        }
    }
}
