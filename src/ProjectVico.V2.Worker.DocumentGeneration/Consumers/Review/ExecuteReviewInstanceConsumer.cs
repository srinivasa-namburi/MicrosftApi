using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Review;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers.Review;

public class ExecuteReviewInstanceConsumer : IConsumer<ExecuteReviewInstance>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly AzureFileHelper _fileHelper;
    private readonly ILogger<ExecuteReviewInstanceConsumer> _logger;

    public ExecuteReviewInstanceConsumer(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        AzureFileHelper fileHelper,
        ILogger<ExecuteReviewInstanceConsumer> logger
        )
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _fileHelper = fileHelper;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<ExecuteReviewInstance> context)
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

        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                "File being retrieved for analysis..."
                ));

        await context.Publish(
            new BackendProcessingMessageGenerated(
                trackedReviewInstance.Id,
                $"SYSTEM:TotalNumberOfQuestions={reviewInstance!.ReviewDefinition!.ReviewQuestions.Count}"
            ));

        var documentAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(reviewInstance.ExportedDocumentLink.Id);

        // We need to retrieve a file stream from the exported document link
        var fileBlobStream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(reviewInstance.ExportedDocumentLink.AbsoluteUrl);

        // For this review instance, we need to index the document attached to it

        // Temporarily disabled until we can check if a document has already been imported
        await _reviewKmRepository.StoreDocumentForReview(reviewInstance.Id, fileBlobStream, reviewInstance.ExportedDocumentLink.FileName, documentAccessUrl);

        // For each question in the review definition, we need to create a review question instance - but that doesn't exist yet
        int answerCount = 0;
        foreach (var reviewQuestion in reviewInstance.ReviewDefinition.ReviewQuestions)
        {
            await context.Publish(
                new BackendProcessingMessageGenerated(
                    trackedReviewInstance.Id,
                    $"SYSTEM:ProcessingQuestionNumber={answerCount+1}"
                ));
            var memoryAnswer = await _reviewKmRepository.AskInDocument(reviewInstance.Id, reviewQuestion);
            var answer = new ReviewQuestionAnswer(reviewQuestion)
            {
                OriginalReviewQuestionId = reviewQuestion.Id,
                FullAiAnswer = memoryAnswer.Result,
                ReviewInstanceId = reviewInstance.Id
            };

            // if the question has been answered before for this instance, delete it
            var existingAnswer = await _dbContext.ReviewQuestionAnswers.FirstOrDefaultAsync(x => x.OriginalReviewQuestionId == reviewQuestion.Id && x.ReviewInstanceId == reviewInstance.Id);
            if (existingAnswer != null)
            {
                _dbContext.ReviewQuestionAnswers.Remove(existingAnswer);
            }

            _dbContext.ReviewQuestionAnswers.Add(answer);

            await _dbContext.SaveChangesAsync();

            //Publish an event that a question has been answered. Include the ReviewQuestionAnswerId and the ReviewInstanceId


            await context.Publish(new ReviewQuestionAnswered(CorrelationId: reviewInstance.Id)
            {
                ReviewQuestionAnswerId = answer.Id,
            });

            answerCount++;

            await context.Publish(
                new BackendProcessingMessageGenerated(
                    trackedReviewInstance.Id,
                    $"SYSTEM:NumberOfQuestionsProcessed={answerCount}"
                ));
        }

        if (answerCount == 0)
        {
            return;
        }

        if (trackedReviewInstance != null)
        {
            // Set the status of the review instance to completed
            trackedReviewInstance.Status = ReviewInstanceStatus.Completed;
            await _dbContext.SaveChangesAsync();

            await context.Publish(
                new BackendProcessingMessageGenerated(
                    trackedReviewInstance.Id,
                    "SYSTEM:ReviewInstanceCompleted"
                ));
        }
    }
}