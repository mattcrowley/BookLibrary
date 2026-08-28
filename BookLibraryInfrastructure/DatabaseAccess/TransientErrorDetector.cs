using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace BookLibrary.Infrastructure.DatabaseAccess
{
    /// <summary>
    /// Decides whether an exception is worth retrying. Has SQL-Server-specific
    /// knowledge (error numbers for throttling, failover, deadlocks, etc.) but
    /// falls back to the provider-agnostic DbException.IsTransient for anything
    /// else, so other providers (Npgsql, MySqlConnector...) get reasonable
    /// behavior for free without this class knowing about them.
    /// </summary>
    public static class TransientErrorDetector
    {
        // Common transient SQL Server error numbers: throttling, failover,
        // "database busy", deadlock victim, timeout, connection broken, etc.
        private static readonly HashSet<int> SqlTransientErrorNumbers = new()
        {
            -2,     // client timeout
            4060,   // cannot open database (e.g. mid-failover)
            4221,   // login to read-secondary while it's catching up
            40197,  // service error, retry
            40501,  // service busy
            40613,  // database unavailable (Azure SQL)
            49918,  // not enough resources (Azure SQL)
            49919,  // too many operations in progress
            49920,  // too many requests
            1205,   // deadlock victim
            233,    // connection initialization error
            64,     // connection forcibly closed
            10053,  // transport-level error
            10054,
            10060,
            11001,  // host not found (transient during DNS blips)
        };

        public static bool IsTransient(Exception ex)
        {
            switch (ex)
            {
                case SqlException sqlEx:
                    foreach (SqlError error in sqlEx.Errors)
                    {
                        if (SqlTransientErrorNumbers.Contains(error.Number))
                            return true;
                    }
                    return false;

                case TimeoutException:
                    return true;

                case SocketException:
                    return true;

                case InvalidOperationException ioEx when
                    ioEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                    ioEx.Message.Contains("closed", StringComparison.OrdinalIgnoreCase):
                    return true;

                // Provider-agnostic fallback (works for Npgsql, MySqlConnector, etc.
                // — DbException.IsTransient has been part of the BCL since .NET 5).
                case DbException dbEx:
                    return dbEx.IsTransient;

                default:
                    return false;
            }
        }
    }
}
