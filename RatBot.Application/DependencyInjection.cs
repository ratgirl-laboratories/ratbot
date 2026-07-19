using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Administration;
using RatBot.Application.Features.EmojiYoink;
using RatBot.Application.Features.Quorum;
using RatBot.Application.Features.Timezone;
using RatBot.Application.MessageContent;
using RatBot.Application.Moderation;
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
            services.AddSingleton(sp => new ImageBurstSpamDetector(TimeProvider.System, sp.GetRequiredService<ImageBurstSpamDetectorSettings>()));

            services.AddScoped<ImageSpamSettingsService>();
            services.AddScoped<AdminSendService>();
            services.AddScoped<ReactionUsageTracker>();
            services.AddScoped<EmojiUsageTracker>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddScoped<EmojiYoinkOperations>();
            services.AddScoped<QuorumOperations>();
            services.AddScoped<UserTimezoneOperations>();
        }
    }
}
