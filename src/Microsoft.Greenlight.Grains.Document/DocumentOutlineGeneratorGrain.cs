using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using System.Text.Json;

namespace Microsoft.Greenlight.Grains.Document;

public class DocumentOutlineGeneratorGrain : Grain, IDocumentOutlineGeneratorGrain
{
    private DocGenerationDbContext _dbContext;
    private readonly IServiceProvider _sp;
    private readonly ILogger<DocumentOutlineGeneratorGrain> _logger;
    
    public DocumentOutlineGeneratorGrain(
        IServiceProvider sp,
        ILogger<DocumentOutlineGeneratorGrain> logger
        )
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task GenerateOutlineAsync(Guid documentId, string documentTitle, string authorOid, string documentProcessName)
    {
        _dbContext = _sp.GetRequiredService<DocGenerationDbContext>();
        var  documentOutlineService = _sp.GetRequiredKeyedService<IDocumentOutlineService>("Dynamic-IDocumentOutlineService");

        try
        {
            _logger.LogInformation("Generating outline for document {DocumentId}", documentId);

            if (string.IsNullOrEmpty(documentProcessName))
            {
                _logger.LogWarning("Blank document process name for document {DocumentId}. Stopping process.", documentId);
                
                var failureGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
                await failureGrain.OnDocumentOutlineGenerationFailedAsync();
                return;
            }

            // Find the document in the database
            var generatedDocument = await _dbContext.GeneratedDocuments.FindAsync(documentId);

            await documentOutlineService.GenerateDocumentOutlineForDocument(generatedDocument!);

            var jsonOutputGeneratedDocument = JsonSerializer.Serialize(generatedDocument);
            _logger.LogInformation("Generated document: {Document}", jsonOutputGeneratedDocument);

            // Notify the orchestration grain that the outline was generated
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            await orchestrationGrain.OnDocumentOutlineGeneratedAsync(jsonOutputGeneratedDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating outline for document {DocumentId}", documentId);
            
            // Notify orchestration grain of failure
            var failureGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            await failureGrain.OnDocumentOutlineGenerationFailedAsync();
        }
    }
}