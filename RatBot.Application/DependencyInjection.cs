using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Administration;
using RatBot.Application.Features.Quorum;
using RatBot.Application.MessageContent;
using RatBot.Application.Moderation;
using RatBot.Application.Quorum;
using RatBot.Application.Reactions;

namespace RatBot.Application;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddApplication()
        {
            services.AddSingleton<ReactionQueue>();
            services.AddSingleton<MessageContentQueue>();
            services.AddSingleton<ImageBurstSpamDetectorSettings>();
            services.AddSingleton<ImageBurstSpamDetector>();

            services.AddScoped<ImageSpamSettingsService>();
            services.AddScoped<AdminSendService>();
            services.AddScoped<ReactionUsageTracker>();
            services.AddScoped<EmojiUsageTracker>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddScoped<QuorumOperations>();
            services.AddScoped<IQuorumSettingsReader, QuorumSettingsReader>();
            services.AddScoped<IQuorumSettingsWriter, QuorumSettingsWriter>();
        }
    }
}
