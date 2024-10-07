using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

public class IndexIngestedDocumentConsumer : IConsumer<IndexIngestedDocument>
{
    private readonly ILogger<IndexIngestedDocumentConsumer> _logger;
    private readonly DocGenerationDbContext _dbContext;
    private readonly AzureFileHelper _fileHelper;
    private readonly IServiceProvider _sp;
    private IRagRepository? _ragRepository;

    public IndexIngestedDocumentConsumer(
        ILogger<IndexIngestedDocumentConsumer> logger,
        DocGenerationDbContext dbContext,
        AzureFileHelper fileHelper, 
        IServiceProvider sp)
    {
        _logger = logger;
        _dbContext = dbContext;
        _fileHelper = fileHelper;
        _sp = sp;
    }

    public async Task Consume(ConsumeContext<IndexIngestedDocument> context)
    {
        var ingestedDocument = await _dbContext.IngestedDocuments
            .Include(x => x.ContentNodes)
            .ThenInclude(r => r.Children)
            .ThenInclude(s => s.Children)
            .ThenInclude(t => t.Children)
            .ThenInclude(u => u.Children)
            .ThenInclude(v => v.Children)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == context.Message.CorrelationId);
        
        if (ingestedDocument == null)
        {
            _logger.LogError("IndexIngestedDocumentConsumer: IngestedDocument with CorrelationId {CorrelationId} not found.", context.Message.CorrelationId);
            return;
        }

        var documentProcess = ingestedDocument.DocumentProcess;

        using var scope = _sp.CreateScope();
        _ragRepository = scope.ServiceProvider.GetKeyedService<IRagRepository>(documentProcess + "-IRagRepository");
        
        if (_ragRepository == null)
        {
            _logger.LogError("IndexIngestedDocumentConsumer: IRagRepository for DocumentProcess {DocumentProcess} not found.", documentProcess);
            return;
        }

        _logger.LogInformation("IndexIngestedDocumentConsumer: Indexing ingested document with CorrelationId {CorrelationId}", context.Message.CorrelationId);
        var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(ingestedDocument.OriginalDocumentUrl);
        
        await _ragRepository.StoreContentNodesAsync(ingestedDocument.ContentNodes, ingestedDocument.FileName, fileStream);
        
        _logger.LogInformation("IndexIngestedDocumentConsumer: Document {CorrelationId} - {FileName} finished indexing", context.Message.CorrelationId, ingestedDocument.Id.ToString());

        ingestedDocument.IngestionState = IngestionState.Complete;
        await _dbContext.SaveChangesAsync();

        await context.Publish(new IngestedDocumentIndexed(ingestedDocument.Id));
    }
}
