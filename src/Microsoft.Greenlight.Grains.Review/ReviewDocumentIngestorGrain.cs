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
        private readonly IContentReferenceVectorRepository _contentReferenceVectorRepository;

        public ReviewDocumentIngestorGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewDocumentIngestorGrain> logger,
            AzureFileHelper fileHelper,
            IContentReferenceService contentReferenceService,
            IContentReferenceVectorRepository contentReferenceVectorRepository)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _fileHelper = fileHelper;
            _contentReferenceService = contentReferenceService;
            _contentReferenceVectorRepository = contentReferenceVectorRepository;
        }

        public async Task<GenericResult<ReviewDocumentIngestionResult>> IngestDocumentAsync()
        {
            var reviewInstanceId = this.GetPrimaryKey();

            try
            {
                _logger.LogInformation("Ingesting document for review instance {ReviewInstanceId}", reviewInstanceId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var reviewInstance = await dbContext.ReviewInstances
                    .Include(x => x.ExternalLinkAsset)
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

                // Get file name and hash from ExternalLinkAsset
                string? displayFileName = reviewInstance.ExternalLinkAsset?.FileName;
                string? fileHash = reviewInstance.ExternalLinkAsset?.FileHash;

                try
                {
                    // Use ContentReferenceService to create the review reference (handles deduplication, RAG text, etc.)
                    var contentReference = await _contentReferenceService.CreateReviewReferenceAsync(
                        reviewInstanceId,
                        displayFileName ?? "Review Document",
                        fileHash);

                    // Index into SK vector store (non-blocking best-effort)
                    try
                    {
                        await _contentReferenceVectorRepository.IndexAsync(contentReference);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Content reference vector indexing failed for review {ReviewId} (best-effort)", reviewInstanceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating content reference for review {ReviewId}", reviewInstanceId);
                }

                // Return success with document info
                return GenericResult<ReviewDocumentIngestionResult>.Success(new ReviewDocumentIngestionResult
                {
                    ExportedDocumentLinkId = reviewInstance.ExternalLinkAsset?.Id,
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
