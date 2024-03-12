using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.Data.Sql;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers;

public class GenerateDocumentOutlineConsumer : IConsumer<GenerateDocumentOutline>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IServiceProvider _sp;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly ILogger<GenerateDocumentOutlineConsumer> _logger;
    private readonly Kernel _sk;
    
    private IDocumentOutlineService _documentOutlineService;
    private DocumentProcessOptions? _documentProcessOptions;

    public GenerateDocumentOutlineConsumer(
        ILogger<GenerateDocumentOutlineConsumer> logger,
        Kernel semanticKernel,
        DocGenerationDbContext dbContext,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IServiceProvider sp)
    {
        _dbContext = dbContext;
        _sp = sp;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
        _sk = semanticKernel;
    }

    [Experimental("SKEXP0060")]
    public async Task Consume(ConsumeContext<GenerateDocumentOutline> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received GenerateDocumentOutline event: {DocumentID}", message.CorrelationId);

        _documentProcessOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.Single(x => x!.Name == message.DocumentProcess);
        if (_documentProcessOptions == null)
        {
           _logger.LogWarning("GenerateDocumentOutlineConsumer: Document process options not found for {ProcessName}. Stopping process for Document {DocumentId}", message.DocumentProcess, message.CorrelationId);
           await context.Publish(new DocumentOutlineGenerationFailed(message.CorrelationId));
        }
        
        var scope = _sp.CreateScope();

        
        _documentOutlineService =
            scope.ServiceProvider.GetKeyedService<IDocumentOutlineService>(_documentProcessOptions.Name + "-IDocumentOutlineService");

        // Find the document in the database
        var generatedDocument = await _dbContext.GeneratedDocuments.FindAsync(message.CorrelationId);
        
        await _documentOutlineService.GenerateDocumentOutlineForDocument(generatedDocument);

        var jsonOutputGeneratedDocument = JsonSerializer.Serialize(generatedDocument);
        // Print the generated document to log output
        _logger.LogInformation("Generated Document: {GeneratedDocument}", jsonOutputGeneratedDocument);

        await context.Publish(new DocumentOutlineGenerated(message.CorrelationId)
        {
            GeneratedDocumentJson = jsonOutputGeneratedDocument,
            AuthorOid = message.AuthorOid
        });

    }


}