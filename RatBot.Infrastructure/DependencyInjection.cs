using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Meta;
using RatBot.Application.Moderation;
using RatBot.Application.Quorum;
using RatBot.Application.Rps;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Persistence.Repositories;
using RatBot.Infrastructure.Stores;

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

            services.AddSingleton<IRpsGameStore, RpsGameStore>();

            services.AddScoped<IMetaSuggestionRepository, MetaSuggestionRepository>();
            services.AddScoped<IMetaSuggestionSettingsRepository, MetaSuggestionSettingsRepository>();
            services.AddScoped<IAutobannedUserRepository, AutobannedUserRepository>();
            services.AddScoped<IQuorumSettingsRepository, QuorumSettingsRepository>();
            services.AddScoped<IEmojiRepository, EmojiRepository>();
        }
    }
}