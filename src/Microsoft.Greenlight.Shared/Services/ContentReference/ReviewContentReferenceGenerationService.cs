// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Services.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Implementation of content reference generation service for review documents
    /// </summary>
    public class ReviewContentReferenceGenerationService : IContentReferenceGenerationService<ReviewInstance>
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ITextExtractionService _textExtractionService;
        private readonly IAiEmbeddingService _aiEmbeddingService;
        private readonly ILogger<ReviewContentReferenceGenerationService> _logger;
        private readonly AzureFileHelper _fileHelper;
        private readonly IFileStorageServiceFactory _fileStorageServiceFactory;

        /// <summary>
        /// Creates a new instance of ReviewContentReferenceGenerationService
        /// </summary>
        public ReviewContentReferenceGenerationService(
            DocGenerationDbContext dbContext,
            ITextExtractionService textExtractionService,
            IAiEmbeddingService aiEmbeddingService,
            AzureFileHelper fileHelper,
            IFileStorageServiceFactory fileStorageServiceFactory,
            ILogger<ReviewContentReferenceGenerationService> logger)
        {
            _dbContext = dbContext;
            _textExtractionService = textExtractionService;
            _aiEmbeddingService = aiEmbeddingService;
            _fileHelper = fileHelper;
            _fileStorageServiceFactory = fileStorageServiceFactory;
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
                // Get the review instance with its exported document (legacy) or asset (new)
                var reviewInstance = await _dbContext.ReviewInstances
                    .Include(r => r.ExportedDocumentLink)
                    .FirstOrDefaultAsync(r => r.Id == reviewId);

                if (reviewInstance == null)
                {
                    _logger.LogWarning("Review {ReviewId} not found", reviewId);
                    return null;
                }

                // Get the document content via FileStorageSource-based service when possible
                try
                {
                    Stream? fileStream = null;
                    string logicalFileName = "review-document";
                    if (reviewInstance.ExportedDocumentLink != null)
                    {
                        // Legacy path: EDL
                        fileStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);
                        logicalFileName = reviewInstance.ExportedDocumentLink.FileName;
                    }
                    else
                    {
                        // New path: ExternalLinkAsset stored into ExportedLinkId field for compatibility
                        var asset = await _dbContext.ExternalLinkAssets.FirstOrDefaultAsync(a => a.Id == reviewInstance.ExportedLinkId);
                        if (asset != null && asset.FileStorageSourceId.HasValue)
                        {
                            // Use storage service for this source
                            var source = await _dbContext.FileStorageSources.Include(s => s.FileStorageHost)
                                .FirstOrDefaultAsync(s => s.Id == asset.FileStorageSourceId.Value);
                            if (source != null)
                            {
                                var sourceInfo = new Contracts.DTO.FileStorage.FileStorageSourceInfo
                                {
                                    Id = source.Id,
                                    Name = source.Name,
                                    ContainerOrPath = source.ContainerOrPath,
                                    AutoImportFolderName = source.AutoImportFolderName,
                                    IsDefault = source.IsDefault,
                                    IsActive = source.IsActive,
                                    ShouldMoveFiles = source.ShouldMoveFiles,
                                    Description = source.Description,
                                    StorageSourceDataType = source.StorageSourceDataType,
                                    FileStorageHost = new Contracts.DTO.FileStorage.FileStorageHostInfo
                                    {
                                        Id = source.FileStorageHost.Id,
                                        Name = source.FileStorageHost.Name,
                                        ProviderType = source.FileStorageHost.ProviderType,
                                        ConnectionString = source.FileStorageHost.ConnectionString,
                                        IsDefault = source.FileStorageHost.IsDefault,
                                        IsActive = source.FileStorageHost.IsActive,
                                        Description = source.FileStorageHost.Description
                                    }
                                };
                                var storageService = _fileStorageServiceFactory.CreateService(sourceInfo);
                                fileStream = await storageService.GetFileStreamAsync(asset.FileName);
                                logicalFileName = Path.GetFileName(asset.FileName);
                            }
                        }
                    }

                    if (fileStream == null)
                    {
                        _logger.LogError("Failed to get file stream for review {ReviewId}", reviewId);
                        return null;
                    }

                    // Extract text from the document using the shared extraction service
                    var documentText = await _textExtractionService.ExtractTextAsync(fileStream, logicalFileName);

                    if (string.IsNullOrEmpty(documentText))
                    {
                        _logger.LogWarning("No text extracted from document for review {ReviewId}", reviewId);
                        return $"Review Document: {reviewInstance.ExportedDocumentLink.FileName}\n" +
                               $"Error: Unable to extract text from document";
                    }

                    // Format the RAG text with document name and content
                    var sb = new StringBuilder();
                    sb.AppendLine($"--- REVIEW DOCUMENT: {logicalFileName} ---");
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
