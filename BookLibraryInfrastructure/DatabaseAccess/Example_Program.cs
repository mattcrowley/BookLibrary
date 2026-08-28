//using System;
//using System.Data;
//using System.Threading.Tasks;
//using DataAccess;

//// This file just demonstrates usage - drop the relevant parts into your own Program.cs / Startup.

//// 1. Register the provider(s) you need once at startup.
//ProviderRegistration.RegisterSqlServer();
//// ProviderRegistration.Register("Npgsql", NpgsqlFactory.Instance); // example for a second provider

//// 2. Build options - swap between LocalDB and SQLEXPRESS just by changing the connection string.
//var options = new DatabaseOptions
//{
//    ProviderName = ProviderRegistration.SqlServer,
//    ConnectionString = SqlServerConnectionStrings.LocalDb("MyAppDb"),
//    // ConnectionString = SqlServerConnectionStrings.SqlExpress("MyAppDb"),
//    MaxRetryAttempts = 5,
//    CommandTimeoutSeconds = 30,
//};

//var db = new Database(options, log: msg => Console.WriteLine(msg));

//// 3. Non-query (DDL/DML).
//await db.ExecuteNonQueryAsync(@"
//    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Widgets')
//    CREATE TABLE Widgets (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL, CreatedUtc DATETIME2 NOT NULL);");

//await db.ExecuteNonQueryAsync(
//    "INSERT INTO Widgets (Name, CreatedUtc) VALUES (@name, @created);",
//    new[]
//    {
//        new DbParameterInfo("@name", "Left-handed smoke shifter", DbType.String),
//        new DbParameterInfo("@created", DateTime.UtcNow, DbType.DateTime2),
//    });

//// 4. Scalar.
//var count = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Widgets;");
//Console.WriteLine($"Widget count: {count}");

//// 5. Reader, mapped straight to your own type.
//var widgets = await db.ExecuteReaderAsync(
//    "SELECT Id, Name, CreatedUtc FROM Widgets ORDER BY Id;",
//    reader => new Widget(
//        reader.GetInt32(0),
//        reader.GetString(1),
//        reader.GetDateTime(2)));

//foreach (var w in widgets)
//    Console.WriteLine($"{w.Id}: {w.Name} ({w.CreatedUtc:u})");

//// 6. Multi-statement transaction.
//await db.ExecuteInTransactionAsync(async (connection, transaction) =>
//{
//    await using var cmd1 = connection.CreateCommand();
//    cmd1.Transaction = transaction;
//    cmd1.CommandText = "UPDATE Widgets SET Name = @name WHERE Id = 1;";
//    var p = cmd1.CreateParameter();
//    p.ParameterName = "@name";
//    p.Value = "Renamed widget";
//    cmd1.Parameters.Add(p);
//    await cmd1.ExecuteNonQueryAsync();

//    await using var cmd2 = connection.CreateCommand();
//    cmd2.Transaction = transaction;
//    cmd2.CommandText = "DELETE FROM Widgets WHERE Id = 999;"; // no-op example
//    await cmd2.ExecuteNonQueryAsync();
//});

//public record Widget(int Id, string Name, DateTime CreatedUtc);
