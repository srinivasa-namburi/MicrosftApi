using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers.Review;

public class AnswerReviewQuestionConsumer : IConsumer<AnswerReviewQuestion>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly ILogger<AnswerReviewQuestionConsumer> _logger;
    private readonly IMapper _mapper;

    public AnswerReviewQuestionConsumer(
        DocGenerationDbContext dbContext,
        IReviewKernelMemoryRepository reviewKmRepository,
        ILogger<AnswerReviewQuestionConsumer> logger,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _reviewKmRepository = reviewKmRepository;
        _logger = logger;
        _mapper = mapper;
    }

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