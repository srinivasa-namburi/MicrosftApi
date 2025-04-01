using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Services.ContentReference;

/// <inheritdoc />
public class GeneratedDocumentReferenceGenerationService : IContentReferenceGenerationService<GeneratedDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<GeneratedDocumentReferenceGenerationService> _logger;
    private readonly IContentNodeService _contentNodeService;
    private readonly IAiEmbeddingService _aiEmbeddingService;

    /// <summary>
    /// Construct the generated document reference generation service.
    /// </summary>
    public GeneratedDocumentReferenceGenerationService(
        DocGenerationDbContext dbContext,
        ILogger<GeneratedDocumentReferenceGenerationService> logger, 
        IContentNodeService contentNodeService,
        IAiEmbeddingService aiEmbeddingService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _contentNodeService = contentNodeService;
        _aiEmbeddingService = aiEmbeddingService;
    }

    /// <inheritdoc />
    public async Task<List<ContentReferenceItemInfo>> GenerateReferencesAsync(GeneratedDocument document)
    {
        return new List<ContentReferenceItemInfo>
        {
            new ContentReferenceItemInfo
            {
                Id = Guid.NewGuid(),
                ContentReferenceSourceId = document.Id,
                DisplayName = document.Title,
                ReferenceType = ContentReferenceType.GeneratedDocument,
                CreatedDate = document.CreatedUtc,
                Description = $"Document: {document.DocumentProcess}"
            }
        };
    }

    /// <inheritdoc />
    public async Task<string?> GenerateContentTextForRagAsync(Guid documentId)
    {
        try
        {
            var document = await _dbContext.GeneratedDocuments
                .Include(generatedDocument => generatedDocument.ContentNodes)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId);
                
            if (document == null)
            {
                _logger.LogWarning("Document with ID {DocumentId} not found", documentId);
                return null;
            }

            var contentNodes =
                await _contentNodeService.GetContentNodesHierarchicalAsyncForDocumentId(
                    documentId, 
                    enableTracking: false,
                    addParentNodes: false);

            if (contentNodes == null || !contentNodes.Any())
            {
                _logger.LogWarning("No content nodes found for document with ID {DocumentId}", documentId);
                return null;
            }

            document.ContentNodes = contentNodes;
            
            // Insert a line initially with the document title and a separator in content Text being produced

            var contentText = "GeneratedDocument: " + document.Title;
            contentText += Environment.NewLine + "------------------------------------" + Environment.NewLine;
            
            contentText += await _contentNodeService.GetRenderedTextForContentNodeHierarchiesAsync(contentNodes);
            contentText += Environment.NewLine + "------------------------------------" + Environment.NewLine;

            
            return contentText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating content text for RAG for document ID {DocumentId}", documentId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingsAsync(Guid documentId)
    {
        try
        {
            var contentText = await GenerateContentTextForRagAsync(documentId);
            if (string.IsNullOrEmpty(contentText))
            {
                _logger.LogWarning("No content text available for document {DocumentId}", documentId);
                return Array.Empty<float>();
            }
                
            // Use the AI embedding service to generate embeddings
            return await _aiEmbeddingService.GenerateEmbeddingsAsync(contentText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for document {DocumentId}", documentId);
            return Array.Empty<float>();
        }
    }
}
