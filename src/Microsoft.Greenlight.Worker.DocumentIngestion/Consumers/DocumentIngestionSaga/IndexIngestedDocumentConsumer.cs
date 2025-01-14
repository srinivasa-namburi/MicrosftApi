using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;



/// <summary>
/// Consumer class for indexing ingested documents.
/// </summary>
public class IndexIngestedDocumentConsumer(
    ILogger<IndexIngestedDocumentConsumer> logger,
    DocGenerationDbContext dbContext,
    AzureFileHelper fileHelper,
    IServiceProvider sp) : IConsumer<IndexIngestedDocument>
{
    private readonly ILogger<IndexIngestedDocumentConsumer> _logger = logger;
    private readonly DocGenerationDbContext _dbContext = dbContext;
    private readonly AzureFileHelper _fileHelper = fileHelper;
    private readonly IServiceProvider _sp = sp;
    private IRagRepository? _ragRepository;

    /// <summary>
    /// Consumes the IndexIngestedDocument message and processes the document.
    /// </summary>
    /// <param name="context">The consume context containing the IndexIngestedDocument message.</param>
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

        if (fileStream == null)
        {
            _logger.LogError("IndexIngestedDocumentConsumer: Document {CorrelationId} - {FileName} not found.", context.Message.CorrelationId, ingestedDocument.FileName);
            return;
        }

        await _ragRepository.StoreContentNodesAsync(ingestedDocument.ContentNodes, ingestedDocument.FileName, fileStream);

        _logger.LogInformation("IndexIngestedDocumentConsumer: Document {CorrelationId} - {FileName} finished indexing", context.Message.CorrelationId, ingestedDocument.Id.ToString());

        ingestedDocument.IngestionState = IngestionState.Complete;
        await _dbContext.SaveChangesAsync();

        await context.Publish(new IngestedDocumentIndexed(ingestedDocument.Id));
    }
}
