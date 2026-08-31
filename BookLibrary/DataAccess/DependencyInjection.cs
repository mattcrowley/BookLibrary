
using BookLibrary.Infrastructure.DatabaseAccess;

namespace BookLibrary.Api.DataAccess
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        {
            ProviderRegistration.RegisterSqlServer();

            // Register DB options so we can use DI for each controller that needs it
            // TODO: db connection relies on config, but provider name still assumes sql server, will eventually need
            // to handle that.
            var dbOptions = new DatabaseOptions
            {
                ProviderName = ProviderRegistration.SqlServer,
                ConnectionString = configuration.GetConnectionString("AppDbContext")
                    ?? SqlServerConnectionStrings.LocalDb("MyAppDb"),
                MaxRetryAttempts = 2,
                CommandTimeoutSeconds = 30,
            };

            services.AddSingleton(dbOptions);
            services.AddSingleton(new Database(dbOptions));

            return services;
        }
    }
}
