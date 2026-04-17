using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Administration;
using RatBot.Application.Meta;
using RatBot.Application.Moderation;
using RatBot.Application.Quorum;
using RatBot.Application.Rps;

namespace RatBot.Application;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddSingleton<EmojiAnalyticsBuffer>();

            services.AddScoped<AdminSendService>();
            services.AddScoped<EmojiAnalyticsService>();
            services.AddScoped<MetaSuggestionService>();
            services.AddScoped<MetaSuggestionSettingsService>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddScoped<QuorumSettingsService>();
            services.AddScoped<RpsGameService>();

            return services;
        }
    }
}