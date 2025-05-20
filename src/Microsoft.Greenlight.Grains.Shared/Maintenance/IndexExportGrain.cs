using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Helpers;
using Npgsql;
using Orleans.Concurrency;
using Pgvector;

namespace Microsoft.Greenlight.Grains.Shared.Maintenance
{
    /// <summary>
    /// Grain that handles exporting Postgres table data to blob storage for backup.
    /// </summary>
    [Reentrant]
    public class IndexExportGrain : Grain, IIndexExportGrain
    {
        private readonly AzureFileHelper _fileHelper;
        private readonly ILogger<IndexExportGrain> _logger;
        private readonly NpgsqlDataSource _dataSource;
        private ISignalRNotifierGrain _notifier;
        private IndexExportJobStatus _status = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexExportGrain"/> class.
        /// </summary>
        public IndexExportGrain(
            AzureFileHelper fileHelper, 
            ILogger<IndexExportGrain> logger, 
            NpgsqlDataSource dataSource)
        {
            _fileHelper  = fileHelper;
            _logger      = logger;
            _dataSource  = dataSource;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            _status.JobId = this.GetPrimaryKey();
            return base.OnActivateAsync(cancellationToken);
        }

        private IndexExportJobNotification ToNotification() =>
            new()
            {
                JobId       = _status.JobId,
                TableName   = _status.TableName,
                BlobUrl     = _status.BlobUrl,
                Error       = _status.Error,
                IsCompleted = _status.IsCompleted,
                IsFailed    = _status.IsFailed,
                Started     = _status.Started,
                Completed   = _status.Completed
            };

        public async Task StartExportAsync(string schema, string tableName, string userGroup)
        {
            _status = new IndexExportJobStatus
            {
                JobId     = this.GetPrimaryKey(),
                TableName = tableName,
                Started   = DateTimeOffset.UtcNow
            };

            string? tempJsonPath = null;
            string? tempZipPath = null;
            try
            {
                _logger.LogInformation("Starting export of {Schema}.{Table} to blob storage", schema, tableName);

                var rows = new List<IndexSectionRow>();

                await using var conn = await _dataSource.OpenConnectionAsync();
                await conn.ReloadTypesAsync();
                await using var cmd = new NpgsqlCommand($"SELECT * FROM {schema}.\"{tableName}\"", conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    object updateValue = reader["_update"];
                    DateTimeOffset? update = null;
                    if (updateValue == DBNull.Value)
                        update = null;
                    else if (updateValue is DateTimeOffset dto)
                        update = dto;
                    else if (updateValue is DateTime dt)
                        update = new DateTimeOffset(dt, TimeSpan.Zero);

                    var row = new IndexSectionRow
                    {
                        _pk = reader["_pk"] as string ?? string.Empty,
                        embedding = reader["embedding"] is Vector v ? v.ToArray() : Array.Empty<float>(),
                        labels = reader["labels"] as string[] ?? Array.Empty<string>(),
                        chunk = reader["chunk"] as string ?? string.Empty,
                        extras = reader["extras"] is JsonElement je ? je : JsonDocument.Parse(reader["extras"].ToString() ?? "{}").RootElement,
                        my_field1 = reader["my_field1"] as string ?? string.Empty,
                        _update = update
                    };
                    rows.Add(row);
                }

                _logger.LogInformation("Retrieved {Count} rows from {Schema}.{Table}", rows.Count, schema, tableName);

                var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
                var fileName = $"{tableName}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                var zipFileName = $"{tableName}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
                tempJsonPath = Path.Combine(Path.GetTempPath(), fileName);
                tempZipPath = Path.Combine(Path.GetTempPath(), zipFileName);
                await File.WriteAllTextAsync(tempJsonPath, json, Encoding.UTF8);

                using (var zip = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tempJsonPath, fileName, CompressionLevel.Optimal);
                }

                await using var zipStream = File.OpenRead(tempZipPath);
                var blobUrl = await _fileHelper.UploadFileToBlobAsync(zipStream, zipFileName, "index-backups", overwriteIfExists:true);
                var url = _fileHelper.GetProxiedBlobUrl(blobUrl);
                
                _status.BlobUrl     = url;
                _status.Completed   = DateTimeOffset.UtcNow;
                _status.IsCompleted = true;

                _logger.LogInformation("Successfully exported {Schema}.{Table} to blob: {Url}", schema, tableName, url);
                await _notifier.NotifyExportJobCompletedAsync(userGroup, ToNotification());
            }
            catch (Exception ex)
            {
                _status.Error     = ex.Message;
                _status.Completed = DateTimeOffset.UtcNow;

                _logger.LogError(ex, "Export failed for {Schema}.{Table}", schema, tableName);
                await _notifier.NotifyExportJobFailedAsync(userGroup, ToNotification());
            }
            finally
            {
                try { if (tempJsonPath != null && File.Exists(tempJsonPath)) File.Delete(tempJsonPath); } catch { }
                try { if (tempZipPath != null && File.Exists(tempZipPath)) File.Delete(tempZipPath); } catch { }
            }
        }

        public Task<IndexExportJobStatus> GetStatusAsync() =>
            Task.FromResult(_status);
    }
}
