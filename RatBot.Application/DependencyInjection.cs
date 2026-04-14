using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Features.AdminSend;
using RatBot.Application.Features.Meta;
using RatBot.Application.Features.Meta.Services;
using RatBot.Application.Features.Quorum;
using RatBot.Application.Features.Rps;

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
            services.AddScoped<QuorumSettingsService>();
            services.AddScoped<RpsGameService>();

            return services;
        }
    }
}
