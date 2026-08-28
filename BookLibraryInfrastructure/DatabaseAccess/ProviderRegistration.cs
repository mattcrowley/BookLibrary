using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace BookLibrary.Infrastructure.DatabaseAccess
{
    /// <summary>
    /// .NET (Core) doesn't auto-populate DbProviderFactories from app.config the
    /// way .NET Framework did, so each provider needs a one-time registration
    /// at startup. Call the one(s) you need before creating a Database instance.
    /// </summary>
    public static class ProviderRegistration
    {
        public const string SqlServer = "Microsoft.Data.SqlClient";

        /// <summary>Registers Microsoft.Data.SqlClient (covers LocalDB, SQLEXPRESS, full SQL Server, Azure SQL).</summary>
        public static void RegisterSqlServer()
        {
            if (!DbProviderFactories.TryGetFactory(SqlServer, out _))
            {
                DbProviderFactories.RegisterFactory(SqlServer, SqlClientFactory.Instance);
            }
        }

        /// <summary>
        /// Generic registration hook for any other ADO.NET provider (Npgsql, MySqlConnector,
        /// System.Data.SQLite, etc). Pass the provider's static Instance factory, e.g.:
        /// ProviderRegistration.Register("Npgsql", NpgsqlFactory.Instance);
        /// </summary>
        public static void Register(string providerInvariantName, DbProviderFactory factory)
        {
            if (!DbProviderFactories.TryGetFactory(providerInvariantName, out _))
            {
                DbProviderFactories.RegisterFactory(providerInvariantName, factory);
            }
        }
    }

    /// <summary>Convenience builders for the two connection targets you asked about.</summary>
    public static class SqlServerConnectionStrings
    {
        /// <summary>
        /// LocalDB — no server process to manage, spins up on first connection.
        /// Good default for dev/test.
        /// </summary>
        public static string LocalDb(string databaseName, string instance = "MSSQLLocalDB") =>
            $@"Server=(localdb)\{instance};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

        /// <summary>
        /// SQL Server Express — a real (lightweight) server instance, usually named
        /// "SQLEXPRESS" on the local machine.
        /// </summary>
        public static string SqlExpress(string databaseName, string server = @".\SQLEXPRESS") =>
            $"Server={server};Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

        /// <summary>Full form for a named server with SQL auth, e.g. a real SQL Server box or Azure SQL.</summary>
        public static string SqlAuth(string server, string databaseName, string userId, string password) =>
            $"Server={server};Database={databaseName};User Id={userId};Password={password};TrustServerCertificate=True;";
    }
}
