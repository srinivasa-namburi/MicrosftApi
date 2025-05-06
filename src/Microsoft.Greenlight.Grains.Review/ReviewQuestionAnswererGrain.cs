using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.SemanticKernel;
using Orleans.Concurrency;
using Scriban;

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewQuestionAnswererGrain : Grain, IReviewQuestionAnswererGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewQuestionAnswererGrain> _logger;
        private readonly IReviewKernelMemoryRepository _reviewKmRepository;
        private readonly IMapper _mapper;
        private readonly IKernelFactory _kernelFactory;
        private readonly IRagContextBuilder _ragContextBuilder;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IContentReferenceService _contentReferenceService;
        private readonly IPromptInfoService _promptInfoService;

        public ReviewQuestionAnswererGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewQuestionAnswererGrain> logger,
            IReviewKernelMemoryRepository reviewKmRepository,
            IKernelFactory kernelFactory,
            IRagContextBuilder ragContextBuilder,
            IDocumentProcessInfoService documentProcessInfoService,
            IContentReferenceService contentReferenceService,
            IPromptInfoService promptInfoService,
            IMapper mapper)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _reviewKmRepository = reviewKmRepository;
            _kernelFactory = kernelFactory;
            _ragContextBuilder = ragContextBuilder;
            _documentProcessInfoService = documentProcessInfoService;
            _contentReferenceService = contentReferenceService;
            _promptInfoService = promptInfoService;
            _mapper = mapper;
        }

        public async Task<GenericResult> AnswerQuestionAsync(Guid reviewInstanceId, ReviewQuestionInfo question)
        {
            try
            {
                _logger.LogInformation("Answering question {QuestionId} for review instance {ReviewInstanceId}",
                    question.Id, reviewInstanceId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Get the review instance to determine document process
                var reviewInstance = await dbContext.ReviewInstances
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

                if (reviewInstance == null)
                {
                    _logger.LogError("Review instance {ReviewInstanceId} not found", reviewInstanceId);
                    return GenericResult.Failure($"Review instance {reviewInstanceId} not found");
                }

                string documentProcessShortName = reviewInstance.DocumentProcessShortName;
                string answer;

                // First try to use the ContentReferenceService approach
                var contentReferenceItems = await GetReviewContentReferenceItemsAsync(reviewInstanceId);

                if (contentReferenceItems.Any())
                {
                    _logger.LogInformation("Using ContentReferenceService approach for review {ReviewInstanceId} with {Count} reference items",
                        reviewInstanceId, contentReferenceItems.Count);

                    answer = await AnswerUsingContentReferenceServiceAsync(question, documentProcessShortName, contentReferenceItems);
                }
                else
                {
                    // Fall back to legacy approach if content references not found
                    _logger.LogWarning("Falling back to Kernel Memory approach for review {ReviewInstanceId}", reviewInstanceId);
                    var memoryAnswer = await _reviewKmRepository.AskInDocument(reviewInstanceId, _mapper.Map<ReviewQuestion>(question));
                    answer = memoryAnswer.Result;
                }

                var answerModel = new ReviewQuestionAnswer()
                {
                    OriginalReviewQuestionId = question.Id,
                    FullAiAnswer = answer,
                    ReviewInstanceId = reviewInstanceId,
                    OriginalReviewQuestionText = question.Question,
                    OriginalReviewQuestionType = question.QuestionType,
                    Order = question.Order,
                    CreatedUtc = question.CreatedUtc != default ? question.CreatedUtc : DateTime.UtcNow
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

        private async Task<List<ContentReferenceItem>> GetReviewContentReferenceItemsAsync(Guid reviewInstanceId)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Try to find a content reference item for this review
                var reviewContentReference = await dbContext.ContentReferenceItems
                    .FirstOrDefaultAsync(r => r.ContentReferenceSourceId == reviewInstanceId &&
                                            r.ReferenceType == ContentReferenceType.ReviewItem);

                if (reviewContentReference != null)
                {
                    // Get the content reference item with its RAG text populated
                    var contentReferenceItem = await _contentReferenceService.GetOrCreateContentReferenceItemAsync(
                        reviewContentReference.Id, ContentReferenceType.ReviewItem);

                    return new List<ContentReferenceItem> { contentReferenceItem };
                }

                return new List<ContentReferenceItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting content reference items for review {ReviewId}", reviewInstanceId);
                return new List<ContentReferenceItem>();
            }
        }

        private async Task<string> AnswerUsingContentReferenceServiceAsync(
            ReviewQuestionInfo question,
            string documentProcessShortName,
            List<ContentReferenceItem> contentReferences)
        {
            // Get document process info to create the appropriate kernel
            var documentProcess = !string.IsNullOrEmpty(documentProcessShortName)
                ? await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessShortName)
                : await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync("Default");

            if (documentProcess == null)
            {
                _logger.LogError("Document process {DocumentProcessName} not found, using fallback approach", documentProcessShortName);
                throw new Exception($"Document process not found for review question answering");
            }

            // Get a kernel specific to this document process
            var kernel = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);
            var promptExecutionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.QuestionAnswering);

            var kernelArguments = new KernelArguments(promptExecutionSettings);

            // Set the number of references based on the information stored in the document process
            var maxReferences = documentProcess.NumberOfCitationsToGetFromRepository;

            // Use the RAG context builder to create context from content references
            string ragContext = await _ragContextBuilder.BuildContextWithSelectedReferencesAsync(
                question.Question,
                contentReferences,
                topN: maxReferences);

            // Create prompt template for answering the question based on question type
            var promptTemplate = question.QuestionType == ReviewQuestionType.Question
                ? await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                    PromptNames.ReviewQuestionAnswerPrompt, documentProcessShortName)
                : await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                    PromptNames.ReviewRequirementAnswerPrompt, documentProcessShortName);

            // Render the prompt using Scriban
            var template = Template.Parse(promptTemplate);
            var renderedPrompt = await template.RenderAsync(new
            {
                question = question.Question,
                context = ragContext,
                questionType = question.QuestionType.ToString()
            }, member => member.Name);

            // Use kernel.InvokePromptAsync with the rendered prompt (and KernelArguments)
            var result = await kernel.InvokePromptAsync(renderedPrompt, kernelArguments);
            return result.GetValue<string>();
        }
    }
}