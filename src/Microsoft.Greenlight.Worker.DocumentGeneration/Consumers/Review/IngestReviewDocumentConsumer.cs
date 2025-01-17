using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review;

/// <summary>
/// A consumer class for the <see cref="IngestReviewDocument"/> message.
/// </summary>
public class IngestReviewDocumentConsumer : IConsumer<IngestReviewDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly AzureFileHelper _fileHelper;
    private readonly ILogger<IngestReviewDocumentConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the IngestReviewDocumentConsumer class.
    /// </summary>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="reviewKmRepository">
    /// The <see cref="IReviewKernelMemoryRepository"/> instance that supports Kernel memory for Reviews.
    /// </param>
    /// <param name="fileHelper">An instance of the <see cref="AzureFileHelper"/> helper class.</param>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    public IngestReviewDocumentConsumer(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        AzureFileHelper fileHelper,
        ILogger<IngestReviewDocumentConsumer> logger)
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _fileHelper = fileHelper;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the <see cref="IngestReviewDocument"/> context.
    /// </summary>
    /// <param name="context">The <see cref="IngestReviewDocument"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<IngestReviewDocument> context)
    {
        var reviewInstance = await _dbContext.ReviewInstances
                .Include(x => x.ExportedDocumentLink)
                .Include(x => x.ReviewDefinition)
                    .ThenInclude(x => x!.ReviewQuestions)
                .Include(x => x.ReviewDefinition)
                    .ThenInclude(x => x!.DocumentProcessDefinitionConnections)
                        .ThenInclude(x => x.DocumentProcessDefinition)
                .Include(x => x.ReviewQuestionAnswers)
                    .ThenInclude(x => x.OriginalReviewQuestion)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == context.Message.CorrelationId)
                ;

        var trackedReviewInstance = await _dbContext.ReviewInstances
            .FirstOrDefaultAsync(x => x.Id == context.Message.CorrelationId);

        if (reviewInstance == null || trackedReviewInstance == null)
        {
            _logger.LogError(
                "ExecuteReviewInstanceConsumer : Review Instance with ID {ReviewInstanceId} could not be found",
                context.Message.CorrelationId);
            return;
        }

        trackedReviewInstance.Status = ReviewInstanceStatus.InProgress;
        await _dbContext.SaveChangesAsync();

        var totalNumberOfQuestions = reviewInstance.ReviewDefinition!.ReviewQuestions.Count;
        
        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                $"SYSTEM:TotalNumberOfQuestions={totalNumberOfQuestions}"
            ));

        if (reviewInstance.ExportedDocumentLink == null)
        {
            _logger.LogError(
                "ExecuteReviewInstanceConsumer : Review Instance with ID {ReviewInstanceId} does not have an ExportedDocumentLink",
                context.Message.CorrelationId);
            return;
        }

        var documentAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(reviewInstance.ExportedDocumentLink.Id);

        // We need to retrieve a file stream from the exported document link
        var fileBlobStream =
            await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);

        if (fileBlobStream == null)
        {
            _logger.LogError(
                "ExecuteReviewInstanceConsumer : Review Instance with ID {ReviewInstanceId} could not retrieve file stream from ExportedDocumentLink",
                context.Message.CorrelationId);
            return;
        }

        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                "File being retrieved for analysis..."
            ));

        await _reviewKmRepository.StoreDocumentForReview(
            reviewInstance.Id, fileBlobStream, reviewInstance.ExportedDocumentLink.FileName, documentAccessUrl);

        await context.Publish(new ReviewDocumentIngested(context.Message.CorrelationId)
        {
            ExportedDocumentLinkId = reviewInstance.ExportedDocumentLink!.Id,
            TotalNumberOfQuestions = totalNumberOfQuestions
        });
    }
}
