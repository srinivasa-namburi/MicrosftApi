using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers.Review;

public class IngestReviewDocumentConsumer : IConsumer<IngestReviewDocument>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly AzureFileHelper _fileHelper;
    private readonly ILogger<IngestReviewDocumentConsumer> _logger;
    private readonly IMapper _mapper;

    public IngestReviewDocumentConsumer(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        AzureFileHelper fileHelper,
        ILogger<IngestReviewDocumentConsumer> logger,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _fileHelper = fileHelper;
        _logger = logger;
        _mapper = mapper;
    }
    public async Task Consume(ConsumeContext<IngestReviewDocument> context)
    {
        var reviewInstance = await _dbContext.ReviewInstances
                .Include(x => x.ExportedDocumentLink)
                .Include(x => x.ReviewDefinition)
                    .ThenInclude(x => x.ReviewQuestions)
                .Include(x => x.ReviewDefinition)
                    .ThenInclude(x => x.DocumentProcessDefinitionConnections)
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
            _logger.LogError("ExecuteReviewInstanceConsumer : Review Instance with ID {ReviewInstanceId} could not be found", context.Message.CorrelationId);
        }

        trackedReviewInstance.Status = ReviewInstanceStatus.InProgress;
        await _dbContext.SaveChangesAsync();

        var totalNumberOfQuestions = reviewInstance!.ReviewDefinition!.ReviewQuestions.Count;
        
        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                $"SYSTEM:TotalNumberOfQuestions={totalNumberOfQuestions}"
            ));

        var documentAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(reviewInstance.ExportedDocumentLink.Id);

        // We need to retrieve a file stream from the exported document link
        var fileBlobStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);

        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                "File being retrieved for analysis..."
            ));

        await _reviewKmRepository.StoreDocumentForReview(reviewInstance.Id, fileBlobStream, reviewInstance.ExportedDocumentLink.FileName, documentAccessUrl);

        await context.Publish(new ReviewDocumentIngested(context.Message.CorrelationId)
        {
            ExportedDocumentLinkId = reviewInstance!.ExportedDocumentLink!.Id,
            TotalNumberOfQuestions = totalNumberOfQuestions
        });
    }
}