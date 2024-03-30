using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class ReindexAllCompletedDocumentsConsumer : IConsumer<ReindexAllCompletedDocuments>
{
    private readonly IIndexingProcessor _indexingProcessor;
    private readonly ILogger<ReindexAllCompletedDocumentsConsumer> _logger;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ServiceConfigurationOptions _options;

    public ReindexAllCompletedDocumentsConsumer(
        IIndexingProcessor indexingProcessor,
        ILogger<ReindexAllCompletedDocumentsConsumer> logger,
        DocGenerationDbContext dbContext,
        IOptions<ServiceConfigurationOptions> options)
    {
        _indexingProcessor = indexingProcessor;
        _logger = logger;
        _dbContext = dbContext;
        _options = options.Value;
    }
    public async Task Consume(ConsumeContext<ReindexAllCompletedDocuments> context)
    {
        var completedDocuments = _dbContext.IngestedDocuments.Where(d => d.IngestionState == IngestionState.Complete);
        if (!completedDocuments.Any())
        {
            _logger.LogWarning("ReindexAllCompletedDocumentsConsumer: No completed documents found to reindex");
            return;
        }
        
        _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Reindexing {count} completed documents", completedDocuments.Count());

        var sectionIndexName = _options.CognitiveSearch.NuclearSectionIndex;
        var titleIndexName = _options.CognitiveSearch.NuclearTitleIndex;
        var customDataIndexName = _options.CognitiveSearch.CustomIndex;

        _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Deleting and recreating all indexes");
        _indexingProcessor.DeleteAllIndexedDocuments(sectionIndexName);
        _indexingProcessor.DeleteAllIndexedDocuments(titleIndexName);
        _indexingProcessor.DeleteAllIndexedDocuments(customDataIndexName);

        _indexingProcessor.CreateIndex(sectionIndexName);
        _indexingProcessor.CreateIndex(titleIndexName);
        _indexingProcessor.CreateIndex(customDataIndexName);
        _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Indexes deleted and recreated");

        foreach (var document in completedDocuments)
        {
            document.IngestionState = IngestionState.Processing;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Reindexing document {documentId}", document.Id);
            await context.Publish(new IndexIngestedDocument(document.Id));
        }

    }
}