using System;

namespace BookLibrary.Infrastructure.DatabaseAccess
{
    /// <summary>
    /// Configuration for a Database instance. ProviderName must match a provider
    /// registered with DbProviderFactories (see ProviderRegistration.cs).
    /// </summary>
    public class DatabaseOptions
    {
        /// <summary>
        /// ADO.NET provider invariant name, e.g. "Microsoft.Data.SqlClient",
        /// "Npgsql", "MySqlConnector", "System.Data.SQLite".
        /// </summary>
        public string ProviderName { get; set; } = "Microsoft.Data.SqlClient";

        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>Max attempts (including the first) before giving up.</summary>
        public int MaxRetryAttempts { get; set; } = 5;

        public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

        public int CommandTimeoutSeconds { get; set; } = 30;
    }
}
