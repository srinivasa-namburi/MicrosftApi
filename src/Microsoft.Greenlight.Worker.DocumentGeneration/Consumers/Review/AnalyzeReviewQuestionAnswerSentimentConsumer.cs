using AutoMapper;
using MassTransit;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Consumers.Review;

/// <summary>
/// A consumer class for the <see cref="AnalyzeReviewQuestionAnswerSentiment"/> message.
/// </summary>
public class AnalyzeReviewQuestionAnswerSentimentConsumer : IConsumer<AnalyzeReviewQuestionAnswerSentiment>
{
    private Kernel _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<AnalyzeReviewQuestionAnswerSentimentConsumer> _logger;
    private readonly IMapper _mapper;
    private readonly IKernelFactory _kernelFactory;

    /// <summary>
    /// Initializes a new instance of the AnalyzeReviewQuestionAnswerSentimentConsumer class.
    /// </summary>
    /// <param name="sk">The Semantic Kernel kernel.</param>
    /// <param name="dbContext">The <see cref="DocGenerationDbContext"/> database context.</param>
    /// <param name="logger">The <see cref="ILogger"/> instance for this class.</param>
    /// <param name="mapper">The AutoMapper mapper instance.</param>
    /// <param name="sp"></param>
    /// <param name="kernelFactory"></param>
    public AnalyzeReviewQuestionAnswerSentimentConsumer(
        DocGenerationDbContext dbContext,
        ILogger<AnalyzeReviewQuestionAnswerSentimentConsumer> logger,
        IMapper mapper,
        IServiceProvider sp,
        IKernelFactory kernelFactory
        )
    {
        _dbContext = dbContext;
        _logger = logger;
        _mapper = mapper;
        _kernelFactory = kernelFactory;
    }

    /// <summary>
    /// Consumes the <see cref="AnalyzeReviewQuestionAnswerSentiment"/> context.
    /// </summary>
    /// <param name="context">The <see cref="AnalyzeReviewQuestionAnswerSentiment"/> context.</param>
    /// <returns>The long-running consuming <see cref="Task"/>.</returns>
    public async Task Consume(ConsumeContext<AnalyzeReviewQuestionAnswerSentiment> context)
    {
        // Using Semantic Kernel, analyze the sentiment of the answer

        _sk = await _kernelFactory.GetGenericKernelAsync("gpt-4o");

        var sentimentScorePrompt = $"""
                      Given the following question:
                      [Question] 
                      {context.Message.ReviewQuestionAnswer.Question}
                      [/Question]
                      
                      And the following answer:
                      [Answer]
                      {context.Message.ReviewQuestionAnswer.AiAnswer}
                      [/Answer]
                      
                      Provide a sentiment on whether the answer is positive, negative, or neutral. Use the following score numeric values:
                      Positive = 100,
                      Negative = 800,
                      Neutral = 999
                      
                      Provide ONLY this number, no introduction, no explanation, no context, just the number.
                      """;

        var kernelResult = await _sk.InvokePromptAsync(sentimentScorePrompt);
        var sentiment = kernelResult.GetValue<string>();

        // Check if the sentiment is valid (it can be cast to an enum value in the enum ReviewQuestionAnswerSentiment)
        if (!Enum.TryParse<ReviewQuestionAnswerSentiment>(sentiment, out var sentimentEnum))
        {
            // If the sentiment is not valid, log an error and return
            _logger.LogWarning(
                "AnalyzeReviewQuestionAnswerSentimentConsumer : Invalid sentiment value {Sentiment} for review question answer {ReviewQuestionAnswerId}",
                sentiment,
                context.Message.ReviewQuestionAnswer.Id);
            return;
        }

        var sentimentReasoningPrompt = $"""
                      Given the following question:
                      [Question] 
                      {context.Message.ReviewQuestionAnswer.Question}
                      [/Question]
                      
                      And the following answer:
                      [Answer]
                      {context.Message.ReviewQuestionAnswer.AiAnswer}
                      [/Answer]
                      
                      You provided the following sentiment of the answer in relation to the question asked:
                      {sentimentEnum.ToString()}
                      
                      Provide a reasoning for the sentiment you provided in plain English. 
                      Be brief, but provide enough context to justify your sentiment.
                      """;

        var sentimentReasoningKernelResult = await _sk.InvokePromptAsync(sentimentReasoningPrompt);
        var sentimentReasoning = sentimentReasoningKernelResult.GetValue<string>();

        var reviewQuestionModel =
            await _dbContext.ReviewQuestionAnswers.FindAsync(context.Message.ReviewQuestionAnswer.Id);

        if (reviewQuestionModel == null)
        {
            // If the review question answer is not found, log an error and return
            _logger.LogWarning(
                "AnalyzeReviewQuestionAnswerSentimentConsumer : Review question answer not found for ID {ReviewQuestionAnswerId}",
                context.Message.ReviewQuestionAnswer.Id);
            return;
        }
        // Update the sentiment of the answer in the database
        reviewQuestionModel.AiSentiment = sentimentEnum;
        reviewQuestionModel.AiSentimentReasoning = sentimentReasoning;

        await _dbContext.SaveChangesAsync();

        var reviewQuestionAnswerWithSentiment = _mapper.Map<ReviewQuestionAnswerInfo>(reviewQuestionModel);

        await context.Publish(new ReviewQuestionAnswerAnalyzed(context.Message.CorrelationId)
        {
            ReviewQuestionAnswerId = reviewQuestionAnswerWithSentiment.Id,
            AnswerWithSentiment = reviewQuestionAnswerWithSentiment
        });

    }
}
