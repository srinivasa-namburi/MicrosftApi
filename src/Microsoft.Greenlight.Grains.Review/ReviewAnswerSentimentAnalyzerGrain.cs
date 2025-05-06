using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Orleans.Concurrency;
using Scriban;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewAnswerSentimentAnalyzerGrain : Grain, IReviewAnswerSentimentAnalyzerGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewAnswerSentimentAnalyzerGrain> _logger;
        private readonly IKernelFactory _kernelFactory;
        private readonly IPromptInfoService _promptInfoService;

        public ReviewAnswerSentimentAnalyzerGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewAnswerSentimentAnalyzerGrain> logger,
            IKernelFactory kernelFactory,
            IPromptInfoService promptInfoService)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _kernelFactory = kernelFactory;
            _promptInfoService = promptInfoService;
        }

        public async Task<GenericResult> AnalyzeSentimentAsync()
        {
            var answerId = this.GetPrimaryKey();

            try
            {
                _logger.LogInformation("Analyzing sentiment for review question answer {AnswerId}", answerId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Get the review question answer
                var reviewQuestionAnswer = await dbContext.ReviewQuestionAnswers
                    .Where(x => x.Id == answerId)
                    .Include(x => x.ReviewInstance)
                    .FirstOrDefaultAsync();

                // Gets the document process name associated with the review instance
                var documentProcessName = reviewQuestionAnswer?.ReviewInstance?.DocumentProcessShortName;

                if (reviewQuestionAnswer == null)
                {
                    return GenericResult.Failure($"Review question answer with ID {answerId} not found");
                }

                // Get kernel for sentiment analysis
                var kernel = await _kernelFactory.GetGenericKernelAsync("gpt-4o");

                var sentimentScorePromptText = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                    PromptNames.ReviewSentimentAnalysisScorePrompt,
                    documentProcessName);


                // Render the prompt using Scriban
                var template = Template.Parse(sentimentScorePromptText);
                var sentimentScorePrompt = await template.RenderAsync(new
                {
                    question = reviewQuestionAnswer.OriginalReviewQuestionText,
                    aiAnswer = reviewQuestionAnswer.FullAiAnswer
                }, member => member.Name);

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

                var sentimentReasoningPromptText = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                    PromptNames.ReviewSentimentReasoningPrompt,
                    documentProcessName);

                var sentimentDecisionString = sentimentEnum.ToString();

                var sentimentReasoningPromptTemplate = Template.Parse(sentimentReasoningPromptText);
                var sentimentReasoningPrompt = await sentimentReasoningPromptTemplate.RenderAsync(new
                {
                    question = reviewQuestionAnswer.OriginalReviewQuestionText,
                    aiAnswer = reviewQuestionAnswer.FullAiAnswer,
                    sentimentDecisionString = sentimentDecisionString
                }, member => member.Name);


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
