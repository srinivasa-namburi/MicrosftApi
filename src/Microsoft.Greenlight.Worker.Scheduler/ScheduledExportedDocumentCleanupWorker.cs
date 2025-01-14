using MassTransit;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.Worker.Scheduler;

/// <summary>
/// Worker service that periodically cleans up exported document links from the database.
/// </summary>
public class ScheduledExportedDocumentCleanupWorker : BackgroundService
{
    private readonly ILogger<ScheduledBlobAutoImportWorker> _logger;
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledExportedDocumentCleanupWorker"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="sp">The service provider.</param>
    public ScheduledExportedDocumentCleanupWorker(
        ILogger<ScheduledBlobAutoImportWorker> logger,
        IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    /// <summary>
    /// Executes the background service operation.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token.</param>
    /// <returns>A task that represents the background service operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = _sp.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

        var taskDelayDefaultMilliseconds = Convert.ToInt32(TimeSpan.FromDays(1).TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ScheduledBlobAutoImportWorker ping: {time}", DateTimeOffset.Now);
            }

            //dbContext.ExportedDocumentLinks
            //    .AsNoTracking()
            //    .Where(edl => edl.Created < DateTime.Now.AddDays(-30) && edl.Type == Shared.Models.DocumentType.ExportedDocument)
            //    .ToList()
            //    .ForEach(async edl =>
            //     await publishEndpoint.Publish(new CleanupExportedDocument(Guid.NewGuid())
            //     {
            //         ExportedDocumentLinkId = edl.Id,
            //         BlobContainer = edl.BlobContainer,
            //         FileName = edl.FileName

            //     }, stoppingToken));

            await Task.Delay(taskDelayDefaultMilliseconds, stoppingToken);
        }
    }
}
