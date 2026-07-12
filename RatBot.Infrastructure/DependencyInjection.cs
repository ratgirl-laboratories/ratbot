using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Features.Quorum;
using RatBot.Application.Features.Timezone;
using RatBot.Application.Moderation;
using RatBot.Application.SecretRole;
using RatBot.Infrastructure.Features.Logging;
using RatBot.Infrastructure.Features.Meta;
using RatBot.Infrastructure.Features.Quorum.Persistence;
using RatBot.Infrastructure.Features.Timezone.Persistence;
using RatBot.Infrastructure.Persistence.Repositories;
using RatBot.Infrastructure.RoleColours;
using RatBot.Infrastructure.SecretRole;
using RatBot.Infrastructure.Stores;

namespace RatBot.Infrastructure;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddInfrastructure(IConfiguration configuration)
        {
            string connectionString = PostgresConnectionStringBuilder.Build(configuration);

            services.AddDbContextFactory<BotDbContext>(options => options.UseNpgsql(connectionString));

            services.AddScoped<IAutobannedUserRepository, AutobannedUserRepository>();
            services.AddScoped<IImageSpamSettingsStore, ImageSpamSettingsStore>();
            services.AddScoped<IQuorumConfigurationStore>(_ => new QuorumConfigurationStore(connectionString));
            services.AddScoped<IUserTimezoneStore>(_ => new UserTimezoneStore(connectionString));
            services.AddScoped<IEmojiRepository>(sp => sp.GetRequiredService<BotDbContext>());
            services.AddScoped<ISecretRoleRepository, SecretRoleRepository>();
            services.AddSingleton<ModerationLoggingStore>();
            services.AddScoped<MetaProposalService>();
            services.AddScoped<MetaSuggestionSettingsService>();
            services.AddScoped<RoleColourOperations>();
        }
    }
}
