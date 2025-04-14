using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewAnswerSentimentAnalyzerGrain : Grain, IReviewAnswerSentimentAnalyzerGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewAnswerSentimentAnalyzerGrain> _logger;
        private readonly IKernelFactory _kernelFactory;

        public ReviewAnswerSentimentAnalyzerGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewAnswerSentimentAnalyzerGrain> logger,
            IKernelFactory kernelFactory)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _kernelFactory = kernelFactory;
        }

        public async Task<GenericResult> AnalyzeSentimentAsync()
        {
            var answerId = this.GetPrimaryKey();

            try
            {
                _logger.LogInformation("Analyzing sentiment for review question answer {AnswerId}", answerId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Get the review question answer
                var reviewQuestionAnswer = await dbContext.ReviewQuestionAnswers.FindAsync(answerId);

                if (reviewQuestionAnswer == null)
                {
                    return GenericResult.Failure($"Review question answer with ID {answerId} not found");
                }

                // Get kernel for sentiment analysis
                var kernel = await _kernelFactory.GetGenericKernelAsync("gpt-4o");

                // Analyze sentiment
                var sentimentScorePrompt = $"""
                      Given the following question:
                      [Question] 
                      {reviewQuestionAnswer.OriginalReviewQuestionText}
                      [/Question]
                      
                      And the following answer:
                      [Answer]
                      {reviewQuestionAnswer.FullAiAnswer}
                      [/Answer]
                      
                      Provide a sentiment on whether the answer is positive, negative, or neutral. Use the following score numeric values:
                      Positive = 100,
                      Negative = 800,
                      Neutral = 999

                      A positive sentiment means the answer is good and relevant to the question asked. You do not need to 
                      look for opinions, just a confirmation that the question has been answered correctly. 
                      
                      A negative sentiment means the answer is negative or irrelevant to the question asked.

                      A neutral sentiment means the answer is neither positive nor negative.

                      "INFO NOT FOUND" means the sentiment should be negative.
                      
                      Provide ONLY this number, no introduction, no explanation, no context, just the number.
                      """;

                var kernelResult = await kernel.InvokePromptAsync(sentimentScorePrompt);
                var sentiment = kernelResult.GetValue<string>();

                // Check if the sentiment is valid
                if (!Enum.TryParse<ReviewQuestionAnswerSentiment>(sentiment, out var sentimentEnum))
                {
                    _logger.LogWarning(
                        "Invalid sentiment value {Sentiment} for review question answer {AnswerId}",
                        sentiment, answerId);
                    
                    return GenericResult.Failure($"Invalid sentiment value: {sentiment}");
                }

                // Get reasoning for the sentiment
                var sentimentReasoningPrompt = $"""
                      Given the following question:
                      [Question] 
                      {reviewQuestionAnswer.OriginalReviewQuestionText}
                      [/Question]
                      
                      And the following answer:
                      [Answer]
                      {reviewQuestionAnswer.FullAiAnswer}
                      [/Answer]
                      
                      You provided the following sentiment of the answer in relation to the question asked:
                      {sentimentEnum.ToString()}
                      
                      Provide a reasoning for the sentiment you provided in plain English. 
                      Be brief, but provide enough context to justify your sentiment.
                      """;

                var sentimentReasoningKernelResult = await kernel.InvokePromptAsync(sentimentReasoningPrompt);
                var sentimentReasoning = sentimentReasoningKernelResult.GetValue<string>();

                // Update the review question answer
                reviewQuestionAnswer.AiSentiment = sentimentEnum;
                reviewQuestionAnswer.AiSentimentReasoning = sentimentReasoning;

                await dbContext.SaveChangesAsync();
                return GenericResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment for review question answer {AnswerId}", answerId);
                return GenericResult.Failure($"Sentiment analysis failed: {ex.Message}");
            }
        }
    }
}
