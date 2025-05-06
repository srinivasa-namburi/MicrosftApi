using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Search;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Implementation of content reference generation service for review documents
    /// </summary>
    public class ReviewContentReferenceGenerationService : IContentReferenceGenerationService<ReviewInstance>
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IKernelMemoryTextExtractionService _textExtractionService;
        private readonly IAiEmbeddingService _aiEmbeddingService;
        private readonly ILogger<ReviewContentReferenceGenerationService> _logger;
        private readonly AzureFileHelper _fileHelper;

        /// <summary>
        /// Creates a new instance of ReviewContentReferenceGenerationService
        /// </summary>
        public ReviewContentReferenceGenerationService(
            DocGenerationDbContext dbContext,
            IKernelMemoryTextExtractionService textExtractionService,
            IAiEmbeddingService aiEmbeddingService,
            AzureFileHelper fileHelper,
            ILogger<ReviewContentReferenceGenerationService> logger)
        {
            _dbContext = dbContext;
            _textExtractionService = textExtractionService;
            _aiEmbeddingService = aiEmbeddingService;
            _fileHelper = fileHelper;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<List<ContentReferenceItemInfo>> GenerateReferencesAsync(ReviewInstance source)
        {
            try
            {
                // Ensure the exported document link is loaded
                if (source.ExportedDocumentLink == null)
                {
                    source = await _dbContext.ReviewInstances
                        .Include(r => r.ExportedDocumentLink)
                        .FirstOrDefaultAsync(r => r.Id == source.Id) ?? source;
                }

                var references = new List<ContentReferenceItemInfo>();

                if (source.ExportedDocumentLink != null)
                {
                    // Create a reference for the review document
                    references.Add(new ContentReferenceItemInfo
                    {
                        Id = Guid.NewGuid(),
                        ContentReferenceSourceId = source.Id,
                        DisplayName = $"Review Document - {source.ExportedDocumentLink.FileName}",
                        Description = $"Document for review {source.Id}",
                        ReferenceType = ContentReferenceType.ReviewItem,
                        CreatedDate = DateTime.UtcNow
                    });
                }

                return references;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating references for review {ReviewId}", source.Id);
                return new List<ContentReferenceItemInfo>();
            }
        }

        /// <inheritdoc />
        public async Task<string?> GenerateContentTextForRagAsync(Guid reviewId)
        {
            try
            {
                // Get the review instance with its exported document
                var reviewInstance = await _dbContext.ReviewInstances
                    .Include(r => r.ExportedDocumentLink)
                    .FirstOrDefaultAsync(r => r.Id == reviewId);

                if (reviewInstance == null || reviewInstance.ExportedDocumentLink == null)
                {
                    _logger.LogWarning("Review {ReviewId} or its exported document not found", reviewId);
                    return null;
                }

                // Get the document content using AzureFileHelper and text extraction service
                try
                {
                    // Get the file stream from AzureFileHelper
                    var fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(
                        reviewInstance.ExportedDocumentLink.AbsoluteUrl);
                        
                    if (fileStream == null)
                    {
                        _logger.LogError("Failed to get file stream for review {ReviewId}", reviewId);
                        return null;
                    }

                    // Extract text from the document using the shared extraction service
                    var documentText = await _textExtractionService.ExtractTextFromDocumentAsync(
                        fileStream,
                        reviewInstance.ExportedDocumentLink.FileName);

                    if (string.IsNullOrEmpty(documentText))
                    {
                        _logger.LogWarning("No text extracted from document for review {ReviewId}", reviewId);
                        return $"Review Document: {reviewInstance.ExportedDocumentLink.FileName}\n" +
                               $"Error: Unable to extract text from document";
                    }

                    // Format the RAG text with document name and content
                    var sb = new StringBuilder();
                    sb.AppendLine($"--- REVIEW DOCUMENT: {reviewInstance.ExportedDocumentLink.FileName} ---");
                    sb.AppendLine();
                    sb.AppendLine(documentText);

                    // Get any answers that may have been generated already for additional context
                    var reviewAnswers = await _dbContext.ReviewQuestionAnswers
                        .Where(a => a.ReviewInstanceId == reviewId)
                        .OrderBy(a => a.CreatedUtc)
                        .ToListAsync();
                    
                    if (reviewAnswers.Any())
                    {
                        sb.AppendLine();
                        sb.AppendLine("--- REVIEW ANSWERS ---");
                        
                        foreach (var answer in reviewAnswers)
                        {
                            sb.AppendLine($"Q: {answer.OriginalReviewQuestionText}");
                            sb.AppendLine($"A: {answer.FullAiAnswer}");
                            sb.AppendLine();
                        }
                    }

                    return sb.ToString().Trim();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract text from document for review {ReviewId}", reviewId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating content text for RAG for review {ReviewId}", reviewId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<float[]> GenerateEmbeddingsAsync(Guid reviewId)
        {
            try
            {
                // Generate RAG text first
                var ragText = await GenerateContentTextForRagAsync(reviewId);
                
                if (string.IsNullOrEmpty(ragText))
                {
                    _logger.LogWarning("Failed to generate RAG text for embeddings for review {ReviewId}", reviewId);
                    return Array.Empty<float>();
                }

                // Generate embeddings
                var embeddings = await _aiEmbeddingService.GenerateEmbeddingsAsync(ragText);
                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embeddings for review {ReviewId}", reviewId);
                return Array.Empty<float>();
            }
        }
    }
}    
