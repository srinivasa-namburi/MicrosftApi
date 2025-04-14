using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Grains.Document;

public class DocumentCreatorGrain : Grain, IDocumentCreatorGrain
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<DocumentCreatorGrain> _logger;

    public DocumentCreatorGrain(
        DocGenerationDbContext dbContext, 
        ILogger<DocumentCreatorGrain> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task CreateDocumentAsync(Guid documentId, GenerateDocumentDTO request)
    {
        try
        {
            _logger.LogInformation("Creating document with ID {DocumentId}", documentId);

            var existingDocument = await _dbContext.GeneratedDocuments.FindAsync(documentId);
            if (existingDocument != null)
            {
                _dbContext.GeneratedDocuments.Remove(existingDocument);
                await _dbContext.SaveChangesAsync();
            }

            var generatedDocument = new GeneratedDocument
            {
                Id = documentId,
                Title = request.DocumentTitle,
                GeneratedDate = DateTime.UtcNow,
                RequestingAuthorOid = new Guid(request.AuthorOid!),
                DocumentProcess = request.DocumentProcessName,
                ContentNodes = []
            };

            var metaData = new DocumentMetadata
            {
                Id = Guid.NewGuid(),
                GeneratedDocumentId = generatedDocument.Id,
                MetadataJson = request.RequestAsJson
            };

            generatedDocument.MetadataId = metaData.Id;

            await _dbContext.GeneratedDocuments.AddAsync(generatedDocument);
            await _dbContext.DocumentMetadata.AddAsync(metaData);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Document with ID {DocumentId} saved to database", documentId);

            // Notify the orchestration grain that the document was created
            var orchestrationGrain = GrainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            await orchestrationGrain.OnDocumentCreatedAsync(metaData.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document with ID {DocumentId}", documentId);
            throw;
        }
    }
}