using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review;

/// <summary>
/// A consumer class for the <see cref="AnswerReviewQuestion"/> message.
/// </summary>
public class AnswerReviewQuestionConsumer : IConsumer<AnswerReviewQuestion>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the AnswerReviewQuestionConsumer class.
    /// </summary>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="reviewKmRepository">
    /// The <see cref="IReviewKernelMemoryRepository"/> instance that supports Kernel memory for Reviews.
    /// </param>
    /// <param name="mapper">The AutoMapper mapper instance.</param>
    public AnswerReviewQuestionConsumer(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// Consumes the <see cref="AnswerReviewQuestion"/> context.
    /// </summary>
    /// <param name="context">The <see cref="AnswerReviewQuestion"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<AnswerReviewQuestion> context)
    {
        var reviewInstanceId = context.Message.CorrelationId;
        var reviewQuestion = context.Message.ReviewQuestion;
        var questionNumber = context.Message.QuestionNumber;

        // Answer the review question
        await context.Publish(
            new BackendProcessingMessageGenerated(
                reviewInstanceId,
                $"SYSTEM:ProcessingQuestionNumber={questionNumber}"
            ));

        
        var memoryAnswer = await _reviewKmRepository.AskInDocument(reviewInstanceId, reviewQuestion);
        var answerModel = new ReviewQuestionAnswer()
        {
            OriginalReviewQuestionId = reviewQuestion.Id,
            FullAiAnswer = memoryAnswer.Result,
            ReviewInstanceId = reviewInstanceId,
            OriginalReviewQuestionText = reviewQuestion.Question,
            OriginalReviewQuestionType = reviewQuestion.QuestionType,
        };
        
        // If the question has been answered before for this instance, delete it
        var existingAnswer = await _dbContext.ReviewQuestionAnswers
            .FirstOrDefaultAsync(x => x.OriginalReviewQuestionId == reviewQuestion.Id &&
                                      x.ReviewInstanceId == reviewInstanceId);

        if (existingAnswer != null)
        {
            _dbContext.ReviewQuestionAnswers.Remove(existingAnswer);
        }

        _dbContext.ReviewQuestionAnswers.Add(answerModel);
        await _dbContext.SaveChangesAsync();

        var answer = _mapper.Map<ReviewQuestionAnswerInfo>(answerModel);
        await context.Publish(new ReviewQuestionAnswered(CorrelationId: reviewInstanceId)
        {
            ReviewQuestionAnswerId = answerModel.Id,
            Answer = answer
        });

        await context.Publish(
            new BackendProcessingMessageGenerated(
                reviewInstanceId,
                $"SYSTEM:QuestionAnswered={questionNumber}"
            ));
    }
}
