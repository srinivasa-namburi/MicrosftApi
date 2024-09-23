using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Data.Sql;

namespace ProjectVico.V2.Worker.Scheduler;

public class ScheduledExportedDocumentCleanupWorker : BackgroundService
{
    private readonly ILogger<ScheduledBlobAutoImportWorker> _logger;
    private readonly IServiceProvider _sp;
    private readonly ServiceConfigurationOptions _options;


    public ScheduledExportedDocumentCleanupWorker(
        ILogger<ScheduledBlobAutoImportWorker> logger,
        IOptions<ServiceConfigurationOptions> options,
        IServiceProvider sp
        
       )
    {
        _logger = logger;
        _sp = sp;
        _options = options.Value;
    }

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
