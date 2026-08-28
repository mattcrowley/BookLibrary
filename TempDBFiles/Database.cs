using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess
{
    public record DbParameterInfo(string Name, object? Value, DbType? DbType = null);

    /// <summary>
    /// Thin, provider-agnostic ADO.NET wrapper. Works against any provider
    /// registered with DbProviderFactories (SQL Server / LocalDB / SQLEXPRESS,
    /// Npgsql, MySqlConnector, SQLite, ...) since it only ever talks to the
    /// DbConnection/DbCommand/DbDataReader base classes.
    ///
    /// Every operation opens its own short-lived connection and is wrapped in
    /// an exponential-backoff-with-jitter retry loop that only retries
    /// transient failures (see TransientErrorDetector).
    /// </summary>
    public class Database
    {
        private readonly DatabaseOptions _options;
        private readonly DbProviderFactory _factory;
        private readonly Action<string>? _log;

        /// <param name="options">Connection + retry configuration.</param>
        /// <param name="log">Optional sink for retry/diagnostic messages (plug in ILogger, Console.WriteLine, etc).</param>
        public Database(DatabaseOptions options, Action<string>? log = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new ArgumentException("ConnectionString must be set.", nameof(options));

            _factory = DbProviderFactories.GetFactory(options.ProviderName);
            _log = log;
        }

        private DbConnection CreateConnection()
        {
            var connection = _factory.CreateConnection()
                ?? throw new InvalidOperationException($"Provider '{_options.ProviderName}' did not return a connection.");
            connection.ConnectionString = _options.ConnectionString;
            return connection;
        }

        public Task<int> ExecuteNonQueryAsync(
            string sql,
            IEnumerable<DbParameterInfo>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteWithRetryAsync(async () =>
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync(ct).ConfigureAwait(false);

                await using var command = CreateCommand(connection, sql, parameters);
                return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }, ct);
        }

        public Task<T?> ExecuteScalarAsync<T>(
            string sql,
            IEnumerable<DbParameterInfo>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteWithRetryAsync(async () =>
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync(ct).ConfigureAwait(false);

                await using var command = CreateCommand(connection, sql, parameters);
                var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);

                if (result is null || result is DBNull)
                    return default;

                return (T)Convert.ChangeType(result, typeof(T));
            }, ct);
        }

        /// <summary>
        /// Executes a query and materializes every row via <paramref name="map"/> before
        /// the connection closes (so the caller never touches a reader tied to a
        /// disposed connection).
        /// </summary>
        public Task<List<T>> ExecuteReaderAsync<T>(
            string sql,
            Func<DbDataReader, T> map,
            IEnumerable<DbParameterInfo>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteWithRetryAsync(async () =>
            {
                var results = new List<T>();

                await using var connection = CreateConnection();
                await connection.OpenAsync(ct).ConfigureAwait(false);

                await using var command = CreateCommand(connection, sql, parameters);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(map(reader));
                }

                return results;
            }, ct);
        }

        /// <summary>
        /// Runs several statements against a single connection/transaction.
        /// The whole callback is retried on a transient failure (so keep it idempotent).
        /// </summary>
        public async Task ExecuteInTransactionAsync(
            Func<DbConnection, DbTransaction, Task> work,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync(ct).ConfigureAwait(false);
                await using var transaction = await connection.BeginTransactionAsync(isolationLevel, ct).ConfigureAwait(false);

                try
                {
                    await work(connection, transaction).ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }

                return true; // ExecuteWithRetryAsync<T> needs a value to return; unused by the caller.
            }, ct).ConfigureAwait(false);
        }

        private static DbCommand CreateCommand(DbConnection connection, string sql, IEnumerable<DbParameterInfo>? parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    var dbParam = command.CreateParameter();
                    dbParam.ParameterName = p.Name;
                    dbParam.Value = p.Value ?? DBNull.Value;
                    if (p.DbType.HasValue)
                        dbParam.DbType = p.DbType.Value;
                    command.Parameters.Add(dbParam);
                }
            }

            return command;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, CancellationToken ct)
        {
            var attempt = 0;

            while (true)
            {
                attempt++;
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < _options.MaxRetryAttempts && TransientErrorDetector.IsTransient(ex))
                {
                    var delay = ComputeDelay(attempt);
                    _log?.Invoke($"Transient DB error on attempt {attempt}/{_options.MaxRetryAttempts} ({ex.GetType().Name}: {ex.Message}). Retrying in {delay.TotalMilliseconds:F0}ms.");
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        private TimeSpan ComputeDelay(int attempt)
        {
            // Exponential backoff (200ms, 400ms, 800ms, ...) capped, plus jitter
            // so many retrying clients don't all wake up in lockstep.
            var exponentialMs = _options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
            var cappedMs = Math.Min(exponentialMs, _options.MaxRetryDelay.TotalMilliseconds);
            var jitterMs = Random.Shared.Next(0, 100);
            return TimeSpan.FromMilliseconds(cappedMs + jitterMs);
        }
    }
}
