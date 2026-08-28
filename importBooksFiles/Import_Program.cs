using DataAccess.Import;

// dotnet run -- "D:\data\ol_dump_works_2024.txt"
var filePath = args.Length > 0 ? args[0] : throw new ArgumentException("Pass the dump file path as the first argument.");
var connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=MyAppDb;Trusted_Connection=True;TrustServerCertificate=True;";
var checkpointPath = filePath + ".checkpoint";

var importer = new OpenLibraryTsvImporter(connectionString, batchSize: 50_000);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Ctrl+C stops cleanly - current batch finishes and checkpoint is written
    // before exit, so re-running picks up right where it left off.
    e.Cancel = true;
    cts.Cancel();
};

await importer.ImportAsync(filePath, checkpointPath, cts.Token);
