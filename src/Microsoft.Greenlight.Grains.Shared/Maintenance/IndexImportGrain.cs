using System.Text.Json;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;
using Npgsql;
using NpgsqlTypes;
using Orleans.Concurrency;
using Pgvector;

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

            string? tempZipPath = null;
            string? tempExtractDir = null;
            string? tempJsonPath = null;
            try
            {
                _logger.LogInformation("Starting import for {Schema}.{Table} from blob: {Url}", schema, tableName, blobUrl);

                // Download the zip file to a temp file
                tempZipPath = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid()}.zip");
                await using (var blobStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(blobUrl))
                await using (var fileStream = File.Create(tempZipPath))
                {
                    await blobStream.CopyToAsync(fileStream);
                }

                // Extract the zip to a temp directory
                tempExtractDir = Path.Combine(Path.GetTempPath(), $"import-{Guid.NewGuid()}");
                Directory.CreateDirectory(tempExtractDir);
                ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir);

                // Find the JSON file in the extracted directory
                tempJsonPath = Directory.GetFiles(tempExtractDir, "*.json").FirstOrDefault();
                if (tempJsonPath == null)
                    throw new Exception("No JSON file found in the extracted zip archive.");

                // Read and deserialize the JSON
                await using var jsonStream = File.OpenRead(tempJsonPath);
                var rows = await JsonSerializer.DeserializeAsync<List<IndexSectionRow>>(jsonStream);
                if (rows == null || !rows.Any())
                    throw new Exception("Backup file is empty or invalid.");

                _logger.LogInformation("Retrieved {Count} rows from backup file", rows.Count);

                await using var conn = await _dataSource.OpenConnectionAsync();
                await conn.ReloadTypesAsync();
                await using var tx = await conn.BeginTransactionAsync();

                try
                {
                    // Truncate the table first
                    _logger.LogInformation("Truncating table {Schema}.{Table}", schema, tableName);
                    await using (var clearCmd = new NpgsqlCommand($"TRUNCATE {schema}.\"{tableName}\"", conn, tx))
                    {
                        await clearCmd.ExecuteNonQueryAsync();
                    }

                    int insertedCount = 0;
                    foreach (var row in rows)
                    {
                        var cmdText = $@"INSERT INTO {schema}.""{tableName}"" (_pk, embedding, labels, chunk, extras, my_field1, _update) VALUES (@pk, @embedding, @labels, @chunk, @extras, @my_field1, @update)";
                        await using var cmd = new NpgsqlCommand(cmdText, conn, tx);
                        cmd.Parameters.AddWithValue("pk", row._pk);
                        cmd.Parameters.AddWithValue("embedding", new Vector(row.embedding));
                        cmd.Parameters.AddWithValue("labels", row.labels);
                        cmd.Parameters.AddWithValue("chunk", row.chunk);
                        cmd.Parameters.AddWithValue("extras", NpgsqlDbType.Jsonb, row.extras.GetRawText());
                        cmd.Parameters.AddWithValue("my_field1", row.my_field1);
                        cmd.Parameters.AddWithValue("update", row._update.HasValue ? row._update.Value : DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                        insertedCount++;
                    }

                    _logger.LogInformation("Inserted {Count} rows into {Schema}.{Table}", insertedCount, schema, tableName);
                    await tx.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during import, rolling back transaction");
                    await tx.RollbackAsync();
                    throw;
                }

                _status.Completed = DateTimeOffset.UtcNow;
                _status.IsCompleted = true;

                _logger.LogInformation("Successfully imported {Schema}.{Table} from blob storage", schema, tableName);
                await _notifier.NotifyImportJobCompletedAsync(userGroup, ToNotification());
            }
            catch (Exception ex)
            {
                _status.Error = ex.Message;
                _status.Completed = DateTimeOffset.UtcNow;
                var statusType = _status.GetType();
                var isFailedProp = statusType.GetProperty("IsFailed");
                if (isFailedProp != null && isFailedProp.CanWrite)
                {
                    isFailedProp.SetValue(_status, true);
                }
                else
                {
                    var isFailedField = statusType.GetField("IsFailed");
                    if (isFailedField != null)
                        isFailedField.SetValue(_status, true);
                }

                _logger.LogError(ex, "Import failed for {Schema}.{Table} from {Url}", schema, tableName, blobUrl);
                await _notifier.NotifyImportJobFailedAsync(userGroup, ToNotification());
            }
            finally
            {
                try { if (tempJsonPath != null && File.Exists(tempJsonPath)) File.Delete(tempJsonPath); } catch { }
                try { if (tempZipPath != null && File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
                try { if (tempExtractDir != null && Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true); } catch { }
            }
        }

        /// <inheritdoc/>
        public Task<IndexImportJobStatus> GetStatusAsync()
        {
            return Task.FromResult(_status);
        }
    }
}
