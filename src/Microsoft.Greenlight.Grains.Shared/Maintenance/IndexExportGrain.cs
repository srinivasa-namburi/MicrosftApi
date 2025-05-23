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
            _fileHelper = fileHelper;
            _logger = logger;
            _dataSource = dataSource;
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
                JobId = _status.JobId,
                TableName = _status.TableName,
                BlobUrl = _status.BlobUrl,
                Error = _status.Error,
                IsCompleted = _status.IsCompleted,
                IsFailed = _status.IsFailed,
                Started = _status.Started,
                Completed = _status.Completed
            };

        public async Task StartExportAsync(string schema, string tableName, string userGroup)
        {
            _status = new IndexExportJobStatus
            {
                JobId = this.GetPrimaryKey(),
                TableName = tableName,
                Started = DateTimeOffset.UtcNow
            };

            const string containerName = "index-backups";
            var fileName = $"{tableName}-{DateTime.UtcNow:yyyyMMddHHmmss}.pgcopy.gz";

            try
            {
                _logger.LogInformation("Starting export of {Schema}.{Table} to blob storage", schema, tableName);

                // 1) Open a raw binary COPY TO STDOUT stream
                await using var conn = await _dataSource.OpenConnectionAsync();
                await using var pgStream = await conn.BeginRawBinaryCopyAsync(
                    $"COPY {schema}.\"{tableName}\" TO STDOUT (FORMAT BINARY)");

                // 2) Open the Azure blob stream and wrap in GZip
                await using var blobStream = await _fileHelper.OpenWriteStreamAsync(
                    containerName, fileName, overwrite: true);
                await using var gzip = new GZipStream(blobStream, CompressionLevel.Optimal, leaveOpen: false);

                // 3) Pump from Postgres → GZip → Azure
                await pgStream.CopyToAsync(gzip, bufferSize: 64 * 1024);
                await pgStream.FlushAsync();
                await gzip.FlushAsync();
                // disposal of pgStream & gzip writes the COPY trailer

                // 4) Build URLs
                var blobClient = _fileHelper
                    .GetBlobServiceClient()
                    .GetBlobContainerClient(containerName)
                    .GetBlobClient(fileName);
                var absoluteUrl = blobClient.Uri.ToString();
                var proxiedUrl = _fileHelper.GetProxiedBlobUrl(absoluteUrl);

                // 5) Update status & notify
                _status.BlobUrl = proxiedUrl;
                _status.Completed = DateTimeOffset.UtcNow;
                _status.IsCompleted = true;

                _logger.LogInformation(
                    "Successfully exported {Schema}.{Table} to blob: {Url}",
                    schema, tableName, proxiedUrl);
                await _notifier.NotifyExportJobCompletedAsync(userGroup, ToNotification());
            }
            catch (Exception ex)
            {
                _status.Error = ex.Message;
                _status.Completed = DateTimeOffset.UtcNow;

                _logger.LogError(ex, "Export failed for {Schema}.{Table}", schema, tableName);
                await _notifier.NotifyExportJobFailedAsync(userGroup, ToNotification());
            }
        }



        public Task<IndexExportJobStatus> GetStatusAsync() =>
            Task.FromResult(_status);
    }
}
