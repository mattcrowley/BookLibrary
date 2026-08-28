
using BookLibrary.Infrastructure.DatabaseAccess;

namespace BookLibrary.Api.DataAccess
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
        {
            ProviderRegistration.RegisterSqlServer();

            services.AddSingleton(new Database(new DatabaseOptions
            {
                ProviderName = ProviderRegistration.SqlServer,
                ConnectionString = configuration.GetConnectionString("Default")
                    ?? SqlServerConnectionStrings.LocalDb("MyAppDb"),
                MaxRetryAttempts = 2,
                CommandTimeoutSeconds = 30,
            }));

            // As you add repositories, register them here too:
            // services.AddScoped<IBookRepository, BookRepository>();
            // services.AddScoped<IAuthorRepository, AuthorRepository>();

            return services;
        }
    }
}
