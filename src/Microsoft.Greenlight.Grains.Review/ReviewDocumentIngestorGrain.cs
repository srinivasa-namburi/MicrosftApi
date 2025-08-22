using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts.Models;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.ContentReference;
// Removed legacy ReviewKernelMemoryRepository dependency
using Orleans.Concurrency;
using System.Security.Cryptography;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewDocumentIngestorGrain : Grain, IReviewDocumentIngestorGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewDocumentIngestorGrain> _logger;
        private readonly AzureFileHelper _fileHelper;
        private readonly IContentReferenceService _contentReferenceService;

        public ReviewDocumentIngestorGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewDocumentIngestorGrain> logger,
            AzureFileHelper fileHelper,
            IContentReferenceService contentReferenceService)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _fileHelper = fileHelper;
            _contentReferenceService = contentReferenceService;
        }

        public async Task<GenericResult<ReviewDocumentIngestionResult>> IngestDocumentAsync()
        {
            var reviewInstanceId = this.GetPrimaryKey();

            try
            {
                _logger.LogInformation("Ingesting document for review instance {ReviewInstanceId}", reviewInstanceId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var reviewInstance = await dbContext.ReviewInstances
                    .Include(x => x.ExportedDocumentLink)
                    .Include(x => x.ReviewDefinition)
                        .ThenInclude(x => x!.ReviewQuestions)
                    .Include(x => x.ReviewDefinition!.DocumentProcessDefinitionConnections
                        .Where(c => c.IsActive))
                    .AsNoTracking()
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

                var trackedReviewInstance = await dbContext.ReviewInstances
                    .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

                if (reviewInstance == null || trackedReviewInstance == null)
                {
                    return GenericResult<ReviewDocumentIngestionResult>.Failure(
                        $"Review Instance with ID {reviewInstanceId} could not be found");
                }

                // Set the document process information if it's not already set
                if (string.IsNullOrEmpty(trackedReviewInstance.DocumentProcessShortName))
                {
                    var activeDocumentProcessConnection = reviewInstance.ReviewDefinition!.DocumentProcessDefinitionConnections
                        .FirstOrDefault(c => c.IsActive);

                    if (activeDocumentProcessConnection != null)
                    {
                        // Get the document process definition to find its shortname
                        var docProcessDefinition = await dbContext.DynamicDocumentProcessDefinitions
                            .FirstOrDefaultAsync(d => d.Id == activeDocumentProcessConnection.DocumentProcessDefinitionId);

                        if (docProcessDefinition != null)
                        {
                            trackedReviewInstance.DocumentProcessShortName = docProcessDefinition.ShortName;
                            trackedReviewInstance.DocumentProcessDefinitionId = docProcessDefinition.Id;
                            _logger.LogInformation("Set document process {DocumentProcessShortName} for review instance {ReviewInstanceId}",
                                docProcessDefinition.ShortName, reviewInstanceId);
                        }
                    }
                }

                trackedReviewInstance.Status = ReviewInstanceStatus.InProgress;
                await dbContext.SaveChangesAsync();

                var totalNumberOfQuestions = reviewInstance.ReviewDefinition!.ReviewQuestions.Count;

                if (reviewInstance.ExportedDocumentLink == null)
                {
                    return GenericResult<ReviewDocumentIngestionResult>.Failure(
                        $"Review Instance with ID {reviewInstanceId} does not have an ExportedDocumentLink");
                }

                var documentAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(reviewInstance.ExportedDocumentLink.Id);

                // We need to retrieve a file stream from the exported document link
                var fileBlobStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);

                if (fileBlobStream == null)
                {
                    return GenericResult<ReviewDocumentIngestionResult>.Failure(
                        $"Could not retrieve file stream from ExportedDocumentLink for review instance {reviewInstanceId}");
                }

                try
                {
                    // Calculate a hash of the file content for deduplication
                    string fileHash;
                    using (var sha256 = SHA256.Create())
                    {
                        fileBlobStream.Position = 0;
                        var hashBytes = await sha256.ComputeHashAsync(fileBlobStream);
                        fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                        // Reset stream position for further use
                        fileBlobStream.Position = 0;
                    }

                    // Create a new ContentReferenceItem for this review
                    var contentReference = new ContentReferenceItem
                    {
                        Id = Guid.NewGuid(), // Generate a new ID for this reference
                        ContentReferenceSourceId = reviewInstanceId, // Link to the review instance
                        DisplayName = reviewInstance.ExportedDocumentLink.FileName,
                        Description = $"Review Document: {reviewInstance.ExportedDocumentLink.FileName}",
                        ReferenceType = ContentReferenceType.ReviewItem,
                        FileHash = fileHash
                    };

                    // Store the content reference in the database
                    dbContext.ContentReferenceItems.Add(contentReference);
                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation("Created content reference {ContentReferenceId} for review {ReviewInstanceId}",
                        contentReference.Id, reviewInstanceId);

                    // Let the content reference service handle generating RAG text and embeddings
                    // We need to update the item directly here since we're only passing in the ID.
                    contentReference = await _contentReferenceService.GetOrCreateContentReferenceItemAsync(contentReference.Id, ContentReferenceType.ReviewItem);

                    // Pre-generate embeddings for this content reference
                    await _contentReferenceService.GetOrCreateEmbeddingsForContentAsync(new List<ContentReferenceItem> { contentReference });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating content reference for review {ReviewId}", reviewInstanceId);
                }

                // Return success with document info
                return GenericResult<ReviewDocumentIngestionResult>.Success(new ReviewDocumentIngestionResult
                {
                    ExportedDocumentLinkId = reviewInstance.ExportedDocumentLink.Id,
                    TotalNumberOfQuestions = totalNumberOfQuestions,
                    DocumentProcessShortName = trackedReviewInstance.DocumentProcessShortName ?? string.Empty,
                    ContentType = ReviewContentType.ExternalFile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting document for review instance {ReviewInstanceId}", reviewInstanceId);
                return GenericResult<ReviewDocumentIngestionResult>.Failure($"Document ingestion failed: {ex.Message}");
            }
        }
    }
}
