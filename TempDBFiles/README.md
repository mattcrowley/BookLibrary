# DataAccess

A small, dependency-light ADO.NET wrapper: async CRUD helpers with automatic
retry on transient failures, built on `System.Data.Common` so it isn't
locked to SQL Server.

## Files

- **DatabaseOptions.cs** — connection string, provider name, retry/timeout settings.
- **ProviderRegistration.cs** — registers ADO.NET provider factories (SQL Server
  helper included), plus `SqlServerConnectionStrings.LocalDb(...)` /
  `.SqlExpress(...)` builders.
- **TransientErrorDetector.cs** — knows SQL Server's transient error numbers
  (throttling, deadlocks, failover, timeouts) and falls back to the BCL's
  provider-agnostic `DbException.IsTransient` for everything else.
- **Database.cs** — the actual client: `ExecuteNonQueryAsync`,
  `ExecuteScalarAsync<T>`, `ExecuteReaderAsync<T>` (row-mapper based),
  `ExecuteInTransactionAsync`. Every call opens its own short-lived
  connection and retries with exponential backoff + jitter.
- **Example_Program.cs** — end-to-end usage against LocalDB/SQLEXPRESS.

## Setup

1. Add the NuGet package for whichever provider(s) you're targeting:
   ```
   dotnet add package Microsoft.Data.SqlClient
   ```
   (add `Npgsql`, `MySqlConnector`, etc. later if you need other databases —
   nothing else in this code changes.)

2. Copy these files into your project (adjust the namespace if you like).

3. At startup:
   ```csharp
   ProviderRegistration.RegisterSqlServer();

   var db = new Database(new DatabaseOptions
   {
       ProviderName = ProviderRegistration.SqlServer,
       ConnectionString = SqlServerConnectionStrings.LocalDb("MyAppDb"),
       // or: SqlServerConnectionStrings.SqlExpress("MyAppDb")
   });
   ```

## Switching to another database later

Everything above `Database.cs` talks only to `DbConnection` / `DbCommand` /
`DbDataReader` base classes — no `SqlConnection` anywhere in the query
methods. To add Postgres, for example:

```csharp
ProviderRegistration.Register("Npgsql", NpgsqlFactory.Instance);

var pgDb = new Database(new DatabaseOptions
{
    ProviderName = "Npgsql",
    ConnectionString = "Host=localhost;Database=myapp;Username=postgres;Password=..."
});
```

Same `Database` class, same retry logic, same call sites.

## Notes / things you may want to tune

- **Connection pooling** is handled by the underlying provider (SqlClient
  pools by connection string automatically) — opening a connection per call
  is cheap and is the recommended ADO.NET pattern, not a perf trap.
- **Retry scope**: retries wrap a whole operation (open + execute), so a
  transient failure mid-query safely restarts from scratch. Keep any work
  passed to `ExecuteInTransactionAsync` idempotent, since it can re-run.
- **Windows Auth vs SQL Auth**: the LocalDB/SQLEXPRESS builders use
  `Trusted_Connection=True` (integrated Windows auth). Use
  `SqlServerConnectionStrings.SqlAuth(...)` for username/password.
- If you'd rather lean on a battle-tested retry library instead of the
  hand-rolled loop here, swapping `ExecuteWithRetryAsync` for a `Polly`
  `AsyncRetryPolicy` is a small, contained change — the rest of the class
  is unaffected.
