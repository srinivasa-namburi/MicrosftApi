// Copyright (c) Microsoft Corporation. All rights reserved.
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;
using Npgsql;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Maintenance
{
    /// <summary>
    /// Grain that handles importing Postgres table data from blob storage for restore.
    /// </summary>
    [Reentrant]
    public class IndexImportGrain : Grain, IIndexImportGrain
    {
        private readonly AzureFileHelper _fileHelper;
        private readonly ILogger<IndexImportGrain> _logger;
        private readonly NpgsqlDataSource _dataSource;
        private ISignalRNotifierGrain _notifier;
        private IndexImportJobStatus _status = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexImportGrain"/> class.
        /// </summary>
        public IndexImportGrain(
            AzureFileHelper fileHelper,
            ILogger<IndexImportGrain> logger,
            NpgsqlDataSource dataSource)
        {
            _fileHelper = fileHelper;
            _logger = logger;
            _dataSource = dataSource;
        }

        /// <inheritdoc/>
        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            _status.JobId = this.GetPrimaryKey();
            return base.OnActivateAsync(cancellationToken);
        }

        /// <summary>
        /// Converts the job status to a notification DTO for SignalR.
        /// </summary>
        private IndexImportJobNotification ToNotification()
        {
            return new IndexImportJobNotification
            {
                JobId = _status.JobId,
                TableName = _status.TableName,
                BlobUrl = _status.BlobUrl,
                Error = _status.Error,
                IsCompleted = _status.IsCompleted,
                IsFailed = _status.IsFailed,
                Started = _status.Started,
                Completed = _status.Completed
            };
        }

        /// <inheritdoc/>
        public async Task StartImportAsync(string schema, string tableName, string blobUrl, string userGroup)
        {
            _status = new IndexImportJobStatus
            {
                JobId = this.GetPrimaryKey(),
                TableName = tableName,
                BlobUrl = blobUrl,
                Started = DateTimeOffset.UtcNow
            };

            try
            {
                _logger.LogInformation(
                    "Starting import for {Schema}.{Table} from blob: {Url}",
                    schema, tableName, blobUrl);

                // 1) Open & decompress the backup blob
                await using var rawBlobStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(blobUrl)
                                           ?? throw new InvalidOperationException("Could not open backup blob.");
                await using var gzip = new GZipStream(rawBlobStream, CompressionMode.Decompress, leaveOpen: false);

                // 2) Connect and reload types
                await using var conn = await _dataSource.OpenConnectionAsync();
                await conn.ReloadTypesAsync();

                // Create a unique name for the temporary table
                string tempTableName = $"{tableName}_import_{DateTime.UtcNow.Ticks}";

                // 3) Clean up any existing temporary tables first
                await CleanupExistingTemporaryTables(conn, schema, tableName);

                // 4) Create temporary table with the same structure as the original
                await CreateTemporaryTableWithSameStructure(conn, schema, tableName, tempTableName);

                // 5) Perform the COPY operation into the temporary table
                {
                    await using var pgStream = await conn.BeginRawBinaryCopyAsync(
                        $"COPY {schema}.\"{tempTableName}\" FROM STDIN (FORMAT BINARY)");

                    // 6) Pump from GZip → Postgres
                    await gzip.CopyToAsync(pgStream, bufferSize: 64 * 1024);
                    await pgStream.FlushAsync();

                    // The COPY operation completes when the stream is disposed by the await using
                }

                // 7) Count rows in the temporary table to verify import was successful
                int rowCount = await CountRowsInTable(conn, schema, tempTableName);
                _logger.LogInformation("Successfully imported {RowCount} rows into temporary table", rowCount);

                // 8) Now in a transaction, swap out the data
                await SwapTableData(conn, schema, tableName, tempTableName);

                // The operation is now complete, set status and notify
                _status.Completed = DateTimeOffset.UtcNow;
                _status.IsCompleted = true;

                _logger.LogInformation(
                    "Successfully imported {Schema}.{Table} from blob storage",
                    schema, tableName);
                await _notifier.NotifyImportJobCompletedAsync(userGroup, ToNotification());
            }
            catch (Exception ex)
            {
                _status.Error = ex.Message;
                _status.Completed = DateTimeOffset.UtcNow;

                // Mark as failed
                var t = _status.GetType();
                var pi = t.GetProperty("IsFailed");
                if (pi != null && pi.CanWrite) pi.SetValue(_status, true);
                else t.GetField("IsFailed")?.SetValue(_status, true);

                _logger.LogError(
                    ex, "Import failed for {Schema}.{Table} from {Url}",
                    schema, tableName, blobUrl);
                await _notifier.NotifyImportJobFailedAsync(userGroup, ToNotification());
            }
        }

        /// <summary>
        /// Cleans up any existing temporary tables that may be left over from previous import attempts
        /// </summary>
        private async Task CleanupExistingTemporaryTables(NpgsqlConnection conn, string schema, string baseTableName)
        {
            _logger.LogInformation("Checking for and cleaning up any existing temporary import tables for {Schema}.{Table}",
                schema, baseTableName);

            // Look for any tables in the schema that match the pattern of our temporary tables
            string pattern = $"{baseTableName}_import_%";

            using var cmd = new NpgsqlCommand(
                @$"
                DO $$
                DECLARE
                    temp_table_record RECORD;
                BEGIN
                    -- Find all tables matching the pattern in this schema
                    FOR temp_table_record IN 
                        SELECT tablename 
                        FROM pg_tables 
                        WHERE schemaname = '{schema}' 
                          AND tablename LIKE '{pattern}'
                    LOOP
                        -- Drop each table found
                        EXECUTE 'DROP TABLE IF EXISTS {schema}.' || quote_ident(temp_table_record.tablename) || ' CASCADE';
                        RAISE NOTICE 'Dropped temporary table: %', temp_table_record.tablename;
                    END LOOP;
                END $$;
                ", conn);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Cleanup of temporary tables completed");
        }

        /// <summary>
        /// Creates a temporary table with the same structure as the original table
        /// </summary>
        private async Task CreateTemporaryTableWithSameStructure(NpgsqlConnection conn, string schema, string originalTable, string tempTable)
        {
            _logger.LogInformation("Creating temporary table {Schema}.{TempTable} with same structure as {Schema}.{OriginalTable}",
                schema, tempTable, schema, originalTable);

            // First check if table exists before attempting to create it
            bool tableExists = await CheckTableExists(conn, schema, tempTable);
            if (tableExists)
            {
                _logger.LogWarning("Temporary table {Schema}.{TempTable} already exists, dropping it first",
                    schema, tempTable);

                // Drop the table if it already exists
                using (var dropCmd = new NpgsqlCommand($"DROP TABLE IF EXISTS {schema}.\"{tempTable}\" CASCADE", conn))
                {
                    await dropCmd.ExecuteNonQueryAsync();
                }
            }

            // Create the temporary table with the same structure as the original
            // Using a two-step approach: create the table structure first, then add indexes separately
            using var cmd = new NpgsqlCommand(
                $@"
                -- Create the table structure without indexes
                CREATE TABLE {schema}.""{tempTable}"" (LIKE {schema}.""{originalTable}"" INCLUDING DEFAULTS INCLUDING CONSTRAINTS EXCLUDING INDEXES);
                ", conn);

            await cmd.ExecuteNonQueryAsync();

            // Now add the indexes separately
            await CreateIndexesForTable(conn, schema, originalTable, tempTable);
        }

        /// <summary>
        /// Checks if a table exists in the database
        /// </summary>
        private async Task<bool> CheckTableExists(NpgsqlConnection conn, string schema, string tableName)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = @schema AND tablename = @tableName)",
                conn);

            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tableName", tableName);

            return (bool)await cmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// Creates the same indexes on the temporary table as exist on the original table
        /// </summary>
        private async Task CreateIndexesForTable(NpgsqlConnection conn, string schema, string originalTable, string tempTable)
        {
            _logger.LogInformation("Creating indexes for temporary table {Schema}.{TempTable}",
                schema, tempTable);

            // Get the list of indexes from the original table and create them on the temp table
            using var cmd = new NpgsqlCommand(
                $@"
                DO $$
                DECLARE
                    index_def text;
                    index_cmd text;
                BEGIN
                    -- For each index on the original table
                    FOR index_def IN 
                        SELECT indexdef 
                        FROM pg_indexes 
                        WHERE schemaname = '{schema}' AND tablename = '{originalTable}'
                    LOOP
                        -- Replace the original table name with the temp table name in the index definition
                        index_cmd := REPLACE(index_def, '{originalTable}', '{tempTable}');

                        -- Log the command we're about to execute
                        RAISE NOTICE 'Executing index creation: %', index_cmd;

                        -- Execute the modified index creation command
                        BEGIN
                            EXECUTE index_cmd;
                            RAISE NOTICE 'Index created successfully';
                        EXCEPTION WHEN OTHERS THEN
                            -- Log errors but continue with other indexes
                            RAISE WARNING 'Error creating index: %', SQLERRM;
                        END;
                    END LOOP;
                END $$;
                ", conn);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Index creation for temporary table completed");
        }

        /// <summary>
        /// Counts the number of rows in a table
        /// </summary>
        private async Task<int> CountRowsInTable(NpgsqlConnection conn, string schema, string tableName)
        {
            using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {schema}.\"{tableName}\"", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Swaps the data between the original table and the temporary table
        /// </summary>
        private async Task SwapTableData(NpgsqlConnection conn, string schema, string originalTable, string tempTable)
        {
            _logger.LogInformation("Swapping data from temporary table {Schema}.{TempTable} to {Schema}.{OriginalTable}",
                schema, tempTable, schema, originalTable);

            // Start a transaction for the data swap
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // Truncate the original table and insert data from the temporary table
                using var cmd = new NpgsqlCommand(
                    $@"
                    -- Truncate the original table
                    TRUNCATE TABLE {schema}.""{originalTable}"";

                    -- Insert data from temporary table to original table
                    INSERT INTO {schema}.""{originalTable}""
                    SELECT * FROM {schema}.""{tempTable}"";

                    -- Drop the temporary table
                    DROP TABLE {schema}.""{tempTable}"" CASCADE;
                    ", conn, tx);

                await cmd.ExecuteNonQueryAsync();

                // Commit the transaction
                await tx.CommitAsync();

                _logger.LogInformation("Data swap completed successfully");
            }
            catch (Exception ex)
            {
                // Rollback the transaction if anything goes wrong
                await tx.RollbackAsync();

                _logger.LogError(ex, "Failed to swap data from temporary table to original table");
                throw new InvalidOperationException($"Failed to swap data: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public Task<IndexImportJobStatus> GetStatusAsync()
        {
            return Task.FromResult(_status);
        }
    }
}
