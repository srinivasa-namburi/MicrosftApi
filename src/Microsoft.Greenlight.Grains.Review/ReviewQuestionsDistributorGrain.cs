using AutoMapper;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Orleans.Concurrency;

public class ReviewQuestionsDistributorGrain : Grain, IReviewQuestionsDistributorGrain
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<ReviewQuestionsDistributorGrain> _logger;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public ReviewQuestionsDistributorGrain(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<ReviewQuestionsDistributorGrain> logger,
        IMapper mapper,
        IConfiguration config)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _mapper = mapper;
        _config = config;
    }

    public async Task<GenericResult> DistributeQuestionsAsync()
    {
        var reviewInstanceId = this.GetPrimaryKey();

        try
        {
            _logger.LogInformation("Distributing questions for review instance {ReviewInstanceId}", reviewInstanceId);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Load the review instance with all necessary data
            var reviewInstance = await dbContext.ReviewInstances
                .Include(x => x.ReviewDefinition)
                    .ThenInclude(x => x!.ReviewQuestions)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

            if (reviewInstance == null ||
                reviewInstance.ReviewDefinition == null ||
                reviewInstance.ReviewDefinition.ReviewQuestions.Count == 0)
            {
                return GenericResult.Failure(
                    $"Review Instance with ID {reviewInstanceId} or its questions could not be found");
            }

            var reviewQuestions = _mapper.Map<List<ReviewQuestionInfo>>(reviewInstance.ReviewDefinition.ReviewQuestions);

            // Get max parallel workers from config or use default of 5
            var maxParallelWorkers = _config.GetValue<int>(
                "ServiceConfiguration:GreenlightServices:Scalability:NumberOfReviewWorkers");

            if (maxParallelWorkers <= 0)
            {
                maxParallelWorkers = 5;
            }

            var semaphore = new SemaphoreSlim(maxParallelWorkers, maxParallelWorkers);

            foreach (var question in reviewQuestions)
            {
                await semaphore.WaitAsync(TimeSpan.FromSeconds(300));

                await Task.Delay(500); // Introduce a half second delay to stagger execution

                var answerQuestionGrain = GrainFactory.GetGrain<IReviewQuestionAnswererGrain>(question.Id);

                // Create a new task for each question
                _ = answerQuestionGrain
                    .AnswerQuestionAsync(reviewInstanceId, question)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception, "Error answering question {QuestionId} for review instance {ReviewInstanceId}",
                                question.Id, reviewInstanceId);
                        }
                        semaphore.Release();
                    });

            }

            _logger.LogInformation("All questions distributed for review instance {ReviewInstanceId}", reviewInstanceId);
            return GenericResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error distributing questions for review instance {ReviewInstanceId}", reviewInstanceId);
            return GenericResult.Failure($"Question distribution failed: {ex.Message}");
        }
    }
}
