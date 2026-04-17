using RatBot.Application;
using RatBot.Discord;
using RatBot.Infrastructure;

namespace RatBot.Host;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddHostServices(IConfiguration configuration)
        {
            services.AddHostedService<DatabaseMigrationHostedService>();

            services.AddApplication();
            services.AddInfrastructure(configuration);
            services.AddDiscordAdapter(configuration);
        }
    }
}