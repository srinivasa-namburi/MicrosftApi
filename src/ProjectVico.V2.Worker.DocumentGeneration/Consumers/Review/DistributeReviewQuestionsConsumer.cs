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

public class DistributeReviewQuestionsConsumer : IConsumer<DistributeReviewQuestions>
{
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<DistributeReviewQuestionsConsumer> _logger;
    private readonly IReviewKernelMemoryRepository _reviewKmRepository;
    private readonly IMapper _mapper;

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
    public async Task Consume(ConsumeContext<DistributeReviewQuestions> context)
    {
        var reviewInstance = await _dbContext.ReviewInstances
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

        if (reviewInstance == null || reviewInstance?.ReviewDefinition?.ReviewQuestions.Count == 0)
        {
            //TODO : Send error event to saga
            _logger.LogError("DistributeReviewQuestionsConsumer : Review Instance with ID {ReviewInstanceId} could not be found", context.Message.CorrelationId);
            return;
        }
        
        var reviewQuestions = _mapper.Map<List<ReviewQuestionInfo>>(
            reviewInstance.ReviewDefinition.ReviewQuestions);
        
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