using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Common;
using RatBot.Application.Common.Interfaces;
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

            // Repositories and unit of work
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BotDbContext>());
            services.AddScoped<IRepository<MetaSuggestionSettings>>(sp => sp.GetRequiredService<BotDbContext>());
            services.AddScoped<IMetaProposalRepository>(sp => sp.GetRequiredService<BotDbContext>());

            services.AddScoped<IAutobannedUserRepository, AutobannedUserRepository>();
            services.AddScoped<IImageSpamSettingsStore, ImageSpamSettingsStore>();
            services.AddScoped<IQuorumSettingsRepository, QuorumSettingsRepository>();
            services.AddScoped<IEmojiRepository>(sp => sp.GetRequiredService<BotDbContext>());
        }
    }
}
