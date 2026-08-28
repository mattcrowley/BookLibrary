using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BookLibrary.Infrastructure.DatabaseAccess;
using Microsoft.Data.SqlClient;

namespace BookImporter
{
    /// <summary>
    /// Streams a tab-delimited OpenLibrary dump (type, key, revision, last_modified, json)
    /// into a staging table via SqlBulkCopy, without ever holding more than one
    /// batch in memory and without modifying the source file.
    ///
    /// Key details:
    ///  - Splits each line on tab with a max count of 5, so any tab/newline that
    ///    happens to be embedded in the JSON blob can't desync the columns
    ///    (this is why we don't use T-SQL BULK INSERT here).
    ///  - Malformed lines are logged to OL_Works_ImportErrors and skipped,
    ///    not fatal to the run.
    ///  - Checkpoints the line number to a small file after every successful
    ///    batch, so a crash/restart resumes instead of re-importing from zero.
    /// </summary>
    public class OpenLibraryTsvImporter
    {
        private readonly string _connectionString;
        private readonly int _batchSize;

        public OpenLibraryTsvImporter(string connectionString, int batchSize = 50_000)
        {
            _connectionString = connectionString;
            _batchSize = batchSize;
        }

        public async Task ImportAsync(string filePath, string checkpointPath, CancellationToken ct = default)
        {
            long resumeFromLine = File.Exists(checkpointPath)
                ? long.Parse(await File.ReadAllTextAsync(checkpointPath, ct))
                : 0;

            var table = CreateStagingSchemaTable();
            long lineNumber = 0;
            long importedCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                ct.ThrowIfCancellationRequested();
                lineNumber++;

                if (lineNumber <= resumeFromLine)
                    continue; // already imported in a previous run

                if (!TryParseLine(line, lineNumber, out var row, out var error))
                {
                    await LogErrorAsync(lineNumber, line, error!, ct);
                    continue;
                }

                table.Rows.Add(row!.RecordType, row.RecordKey, row.Revision, row.LastModified, row.RawJson, lineNumber);

                if (table.Rows.Count >= _batchSize)
                {
                    await WriteBatchWithRetryAsync(table, ct);
                    importedCount += table.Rows.Count;
                    table.Clear();

                    await File.WriteAllTextAsync(checkpointPath, lineNumber.ToString(), ct);
                    Console.WriteLine($"Imported {importedCount:N0} rows (line {lineNumber:N0}), {sw.Elapsed.TotalSeconds:F0}s elapsed.");
                }
            }

            if (table.Rows.Count > 0)
            {
                await WriteBatchWithRetryAsync(table, ct);
                importedCount += table.Rows.Count;
                await File.WriteAllTextAsync(checkpointPath, lineNumber.ToString(), ct);
            }

            Console.WriteLine($"Done. {importedCount:N0} rows imported, {lineNumber:N0} lines read, {sw.Elapsed.TotalMinutes:F1} minutes.");
        }

        private static DataTable CreateStagingSchemaTable()
        {
            var table = new DataTable();
            table.Columns.Add("RecordType", typeof(string));
            table.Columns.Add("RecordKey", typeof(string));
            table.Columns.Add("Revision", typeof(int));
            table.Columns.Add("LastModified", typeof(DateTime));
            table.Columns.Add("RawJson", typeof(string));
            table.Columns.Add("SourceLine", typeof(long));
            return table;
        }

        private sealed record ParsedRow(string RecordType, string RecordKey, object Revision, object LastModified, string RawJson);

        private static bool TryParseLine(string line, long lineNumber, out ParsedRow? row, out string? error)
        {
            row = null;
            error = null;

            // Max count of 5: everything from the 4th tab onward (the JSON) stays
            // together even if it contains raw tab/control characters.
            var parts = line.Split('\t', 5);

            if (parts.Length != 5)
            {
                error = $"Expected 5 tab-separated fields, got {parts.Length}.";
                return false;
            }

            var recordType = parts[0];
            var recordKey = parts[1];
            var revisionText = parts[2];
            var lastModifiedText = parts[3];
            var json = parts[4]; // TODO we should parse this for some extra fields, like description, that are useful. some repeated fields here we do not need too

            object revision = int.TryParse(revisionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rev)
                ? rev
                : DBNull.Value;

            object lastModified = DateTime.TryParse(lastModifiedText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : DBNull.Value;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty JSON payload.";
                return false;
            }

            row = new ParsedRow(recordType, recordKey, revision, lastModified, json);
            return true;
        }

        private async Task WriteBatchWithRetryAsync(DataTable table, CancellationToken ct)
        {
            const int maxAttempts = 5;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync(ct);

                    using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, externalTransaction: null)
                    {
                        DestinationTableName = "dbo.OL_Works_Staging",
                        BatchSize = _batchSize,
                        BulkCopyTimeout = 300,
                    };

                    bulkCopy.ColumnMappings.Add("RecordType", "RecordType");
                    bulkCopy.ColumnMappings.Add("RecordKey", "RecordKey");
                    bulkCopy.ColumnMappings.Add("Revision", "Revision");
                    bulkCopy.ColumnMappings.Add("LastModified", "LastModified");
                    bulkCopy.ColumnMappings.Add("RawJson", "RawJson");
                    bulkCopy.ColumnMappings.Add("SourceLine", "SourceLine");

                    await bulkCopy.WriteToServerAsync(table, ct);
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts && TransientErrorDetector.IsTransient(ex))
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Transient error on batch write (attempt {attempt}/{maxAttempts}): {ex.Message}. Retrying in {delay.TotalSeconds}s.");
                    await Task.Delay(delay, ct);
                }
            }
        }

        private async Task LogErrorAsync(long lineNumber, string rawLine, string errorMessage, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO dbo.OL_Works_ImportErrors (SourceLine, RawLine, ErrorMessage)
                VALUES (@line, @raw, @err);";

            command.Parameters.AddWithValue("@line", lineNumber);
            command.Parameters.AddWithValue("@raw", rawLine.Length > 8000 ? rawLine[..8000] : rawLine);
            command.Parameters.AddWithValue("@err", errorMessage);

            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
