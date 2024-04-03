using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

public class IndexIngestedDocumentConsumer : IConsumer<IndexIngestedDocument>
{
    private readonly ILogger<IndexIngestedDocumentConsumer> _logger;
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly DocGenerationDbContext _dbContext;
    private readonly AzureFileHelper _fileHelper;

    public IndexIngestedDocumentConsumer(
        ILogger<IndexIngestedDocumentConsumer> logger,
        IIndexingProcessor indexingProcessor,
        DocGenerationDbContext dbContext,
        AzureFileHelper fileHelper

        )
    {
        _logger = logger;
        _indexingProcessor = indexingProcessor;
        _dbContext = dbContext;
        _fileHelper = fileHelper;
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

        _logger.LogInformation("IndexIngestedDocumentConsumer: Indexing ingested document with CorrelationId {CorrelationId}", context.Message.CorrelationId);
        var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(ingestedDocument.OriginalDocumentUrl);
        await _indexingProcessor.IndexAndStoreContentNodesAsync(ingestedDocument.ContentNodes, ingestedDocument.FileName, fileStream);
        
        _logger.LogInformation("IndexIngestedDocumentConsumer: Document {CorrelationId} - {FileName} finished indexing", context.Message.CorrelationId, ingestedDocument.Id.ToString());

        ingestedDocument.IngestionState = IngestionState.Complete;
        await _dbContext.SaveChangesAsync();

        await context.Publish(new IngestedDocumentIndexed(ingestedDocument.Id));
    }
}