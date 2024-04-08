using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers;

public class ReindexAllCompletedDocumentsConsumer : IConsumer<ReindexAllCompletedDocuments>
{
    private readonly ILogger<ReindexAllCompletedDocumentsConsumer> _logger;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IServiceProvider _sp;
    private readonly ServiceConfigurationOptions _options;

    public ReindexAllCompletedDocumentsConsumer(
        ILogger<ReindexAllCompletedDocumentsConsumer> logger,
        DocGenerationDbContext dbContext,
        IOptions<ServiceConfigurationOptions> options,
        IServiceProvider sp)
    {
        _logger = logger;
        _dbContext = dbContext;
        _sp = sp;
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

        var documentProcesses = _options.ProjectVicoServices.DocumentProcesses;

        foreach (var documentProcess in documentProcesses)
        {
            using var scope = _sp.CreateScope();
            var ragRepository = scope.ServiceProvider.GetKeyedService<IRagRepository>(documentProcess.Name + "-IRagRepository");

            var documentProcessDocuments = completedDocuments.Where(d => d.DocumentProcess == documentProcess.Name);
            
            if (ragRepository == null)
            {
                _logger.LogError("ReindexAllCompletedDocumentsConsumer: IRagRepository for DocumentProcess {DocumentProcess} not found.", documentProcess.Name);
                return;
            }

            _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Clearing and recreating repositories for Document Process {DocumentProcess}", documentProcess.Name);

            ragRepository.ClearRepositoryContent();
            ragRepository.CreateOrUpdateRepository();

            _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Reindexing {count} completed documents for Document Process {DocumentProcess}", documentProcessDocuments.Count(), documentProcess.Name);

            foreach (var document in documentProcessDocuments)
            {
                document.IngestionState = IngestionState.Processing;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("ReindexAllCompletedDocumentsConsumer: Reindexing document {documentId}", document.Id);
                await context.Publish(new IndexIngestedDocument(document.Id));
            }
        }
    }
}