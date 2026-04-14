using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Features.Meta;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Application.Features.Quorum;
using RatBot.Application.Features.Rps;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Persistence;
using RatBot.Infrastructure.Settings;
using RatBot.Infrastructure.Settings.Meta;
using RatBot.Infrastructure.Settings.Quorum;

namespace RatBot.Infrastructure;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructure(IConfiguration configuration)
        {
            string connectionString = PostgresConnectionStringBuilder.Build(configuration);

            services.AddDbContext<BotDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContextFactory<BotDbContext>(options => options.UseNpgsql(connectionString));

            services.AddScoped<IBotDataContext>(sp => sp.GetRequiredService<BotDbContext>());

            services.AddSingleton<IRpsGameStore, RpsGameStore>();
            services.AddScoped<IMetaSuggestionRepository, MetaSuggestionRepository>();
            services.AddScoped<IMetaSuggestionSettingsRepository, MetaSuggestionSettingsRepository>();
            services.AddScoped<IQuorumSettingsRepository, QuorumSettingsRepository>();
        }
    }
}
