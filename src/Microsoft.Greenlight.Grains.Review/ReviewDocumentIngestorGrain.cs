using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts.Models;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services.Search;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewDocumentIngestorGrain : Grain, IReviewDocumentIngestorGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewDocumentIngestorGrain> _logger;
        private readonly IReviewKernelMemoryRepository _reviewKmRepository;
        private readonly AzureFileHelper _fileHelper;

        public ReviewDocumentIngestorGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewDocumentIngestorGrain> logger,
            IReviewKernelMemoryRepository reviewKmRepository,
            AzureFileHelper fileHelper)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _reviewKmRepository = reviewKmRepository;
            _fileHelper = fileHelper;
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
                var fileBlobStream =
                    await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);

                if (fileBlobStream == null)
                {
                    return GenericResult<ReviewDocumentIngestionResult>.Failure(
                        $"Could not retrieve file stream from ExportedDocumentLink for review instance {reviewInstanceId}");
                }

                await _reviewKmRepository.StoreDocumentForReview(
                    reviewInstance.Id, fileBlobStream, reviewInstance.ExportedDocumentLink.FileName, documentAccessUrl);

                // Return success with document info
                return GenericResult<ReviewDocumentIngestionResult>.Success(new ReviewDocumentIngestionResult
                {
                    ExportedDocumentLinkId = reviewInstance.ExportedDocumentLink.Id,
                    TotalNumberOfQuestions = totalNumberOfQuestions
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
