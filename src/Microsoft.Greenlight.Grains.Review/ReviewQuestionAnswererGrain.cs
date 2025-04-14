using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Services.Search;
using Orleans.Concurrency;

[StatelessWorker]
public class ReviewQuestionAnswererGrain : Grain, IReviewQuestionAnswererGrain
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<ReviewQuestionAnswererGrain> _logger;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly IMapper _mapper;

    public ReviewQuestionAnswererGrain(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<ReviewQuestionAnswererGrain> logger,
        IReviewKernelMemoryRepository reviewKmRepository,
        IMapper mapper)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _reviewKmRepository = reviewKmRepository;
        _mapper = mapper;
    }

    public async Task<GenericResult> AnswerQuestionAsync(Guid reviewInstanceId, ReviewQuestionInfo question)
    {
        try
        {
            _logger.LogInformation("Answering question {QuestionId} for review instance {ReviewInstanceId}",
                question.Id, reviewInstanceId);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Answer the question using RAG
            var memoryAnswer = await _reviewKmRepository.AskInDocument(reviewInstanceId, _mapper.Map<ReviewQuestion>(question));

            var answerModel = new ReviewQuestionAnswer()
            {
                OriginalReviewQuestionId = question.Id,
                FullAiAnswer = memoryAnswer.Result,
                ReviewInstanceId = reviewInstanceId,
                OriginalReviewQuestionText = question.Question,
                OriginalReviewQuestionType = question.QuestionType,
            };

            // If the question has been answered before for this instance, delete it
            var existingAnswer = await dbContext.ReviewQuestionAnswers
                .FirstOrDefaultAsync(x => x.OriginalReviewQuestionId == question.Id &&
                                         x.ReviewInstanceId == reviewInstanceId);

            if (existingAnswer != null)
            {
                dbContext.ReviewQuestionAnswers.Remove(existingAnswer);
            }

            dbContext.ReviewQuestionAnswers.Add(answerModel);
            await dbContext.SaveChangesAsync();

            // Notify orchestration grain that a question has been answered
            var orchestrationGrain = GrainFactory.GetGrain<IReviewExecutionOrchestrationGrain>(reviewInstanceId);
            await orchestrationGrain.OnQuestionAnsweredAsync(answerModel.Id);

            return GenericResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering question {QuestionId} for review instance {ReviewInstanceId}",
                question.Id, reviewInstanceId);
            return GenericResult.Failure($"Failed to answer question {question.Id}: {ex.Message}");
        }
    }
}
