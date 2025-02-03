using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review;

/// <summary>
/// A consumer class for the <see cref="DistributeReviewQuestions"/> message.
/// </summary>
public class DistributeReviewQuestionsConsumer : IConsumer<DistributeReviewQuestions>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<DistributeReviewQuestionsConsumer> _logger;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the DistributeReviewQuestionsConsumer class.
    /// </summary>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    /// <param name="reviewKmRepository">
    /// The <see cref="IReviewKernelMemoryRepository"/> instance that supports Kernel memory for Reviews.
    /// </param>
    /// <param name="mapper">The AutoMapper mapper instance.</param>
    public DistributeReviewQuestionsConsumer(
        DocGenerationDbContext dbContext,
        ILogger<DistributeReviewQuestionsConsumer> logger,
        IReviewKernelMemoryRepository reviewKmRepository,
        IMapper mapper
        )
    {
        _dbContext = dbContext;
        _logger = logger;
        _reviewKmRepository = reviewKmRepository;
        _mapper = mapper;
    }

    /// <summary>
    /// Consumes the <see cref="DistributeReviewQuestions"/> context.
    /// </summary>
    /// <param name="context">The <see cref="DistributeReviewQuestions"/> context.</param>
    /// <returns>The long running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<DistributeReviewQuestions> context)
    {
        var reviewInstance = await _dbContext.ReviewInstances
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

        if (reviewInstance == null
            || reviewInstance.ReviewDefinition == null
            || reviewInstance.ReviewDefinition.ReviewQuestions.Count == 0)
        {
            //TODO : Send error event to saga
            _logger.LogError(
                "DistributeReviewQuestionsConsumer : Review Instance with ID {ReviewInstanceId} could not be found",
                context.Message.CorrelationId);
            return;
        }
        
        var reviewQuestions = _mapper.Map<List<ReviewQuestionInfo>>(reviewInstance.ReviewDefinition.ReviewQuestions);
        
        var questionNumber = 0;
        var totalNumberOfQuestions = reviewQuestions.Count;

        foreach (var reviewQuestion in reviewQuestions)
        {
            questionNumber++;
            await context.Publish(new AnswerReviewQuestion(context.Message.CorrelationId)
            {
                ReviewQuestion = reviewQuestion,
                QuestionNumber = questionNumber,
                TotalQuestions = totalNumberOfQuestions
            });
        }
    }
}
