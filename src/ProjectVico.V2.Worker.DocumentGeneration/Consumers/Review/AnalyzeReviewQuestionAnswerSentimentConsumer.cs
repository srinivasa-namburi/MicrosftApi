using AutoMapper;
using Elastic.Transport.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.Review.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models.Review;

namespace ProjectVico.V2.Worker.DocumentGeneration.Consumers.Review;

public class AnalyzeReviewQuestionAnswerSentimentConsumer : IConsumer<AnalyzeReviewQuestionAnswerSentiment>
{
    private readonly Kernel _sk;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<AnalyzeReviewQuestionAnswerSentimentConsumer> _logger;
    private readonly IMapper _mapper;

    public AnalyzeReviewQuestionAnswerSentimentConsumer(
        Kernel sk,
        DocGenerationDbContext dbContext,
        ILogger<AnalyzeReviewQuestionAnswerSentimentConsumer> logger,
        IMapper mapper
        )
    {
        _sk = sk;
        _dbContext = dbContext;
        _logger = logger;
        _mapper = mapper;
    }
    public async Task Consume(ConsumeContext<AnalyzeReviewQuestionAnswerSentiment> context)
    {
        // Using Semantic Kernel, analyze the sentiment of the answer

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
            _logger.LogWarning("AnalyzeReviewQuestionAnswerSentimentConsumer : Invalid sentiment value {Sentiment} for review question answer {ReviewQuestionAnswerId}", sentiment, context.Message.ReviewQuestionAnswer.Id);
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
                      {sentimentEnum.GetStringValue()}
                      
                      Provide a reasoning for the sentiment you provided in plain English. 
                      Be brief, but provide enough context to justify your sentiment.
                      """;

        var sentimentReasoningKernelResult = await _sk.InvokePromptAsync(sentimentReasoningPrompt);
        var sentimentReasoning = sentimentReasoningKernelResult.GetValue<string>();

        var reviewQuestionModel = await _dbContext.ReviewQuestionAnswers.FindAsync(context.Message.ReviewQuestionAnswer.Id);

        if (reviewQuestionModel == null)
        {
            // If the review question answer is not found, log an error and return
            _logger.LogWarning("AnalyzeReviewQuestionAnswerSentimentConsumer : Review question answer not found for ID {ReviewQuestionAnswerId}", context.Message.ReviewQuestionAnswer.Id);
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
