using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Review.Contracts.Models;
using Microsoft.Greenlight.Grains.Review.Contracts.State;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Review
{
    /// <summary>
    /// Orchestrates the review execution process, replacing the MassTransit-based ReviewExecutionSaga
    /// </summary>
    [Reentrant]
    public class ReviewExecutionOrchestrationGrain : Grain, IReviewExecutionOrchestrationGrain
    {
        private readonly IPersistentState<ReviewExecutionState> _state;
        private readonly ILogger<ReviewExecutionOrchestrationGrain> _logger;
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        public ReviewExecutionOrchestrationGrain(
            [PersistentState("reviewExecution")]
            IPersistentState<ReviewExecutionState> state,
            ILogger<ReviewExecutionOrchestrationGrain> logger,
            IDbContextFactory<DocGenerationDbContext> dbContextFactory)
        {
            _state = state;
            _logger = logger;
            _dbContextFactory = dbContextFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State.Id == Guid.Empty)
            {
                _state.State.Id = this.GetPrimaryKey();
                await SafeWriteStateAsync();
            }

            await base.OnActivateAsync(cancellationToken);
        }

        public Task<ReviewExecutionState> GetStateAsync() => Task.FromResult(_state.State);

        public async Task ExecuteReviewAsync(ExecuteReviewRequest request)
        {
            try
            {
                _logger.LogInformation("Starting review execution for review instance {Id}", this.GetPrimaryKey());

                // Store initial state
                _state.State.ReviewInstanceId = this.GetPrimaryKey();
                _state.State.Status = ReviewExecutionStatus.Started;
                _state.State.TotalNumberOfQuestions = 0;
                _state.State.NumberOfQuestionsAnswered = 0;
                _state.State.NumberOfQuestionsAnalyzed = 0;
                // Standardize on StartedByProviderSubjectId for per-user context
                if (string.IsNullOrWhiteSpace(_state.State.StartedByProviderSubjectId) && !string.IsNullOrWhiteSpace(request.ProviderSubjectId))
                {
                    _state.State.StartedByProviderSubjectId = request.ProviderSubjectId;
                }
                await SafeWriteStateAsync();

                // First, check if the review has a document to ingest
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var reviewInstance = await dbContext.ReviewInstances
                    .Include(x => x.ExportedDocumentLink)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == this.GetPrimaryKey());

                if (reviewInstance == null)
                {
                    await HandleFailureAsync("Failed to find review instance", "Review instance not found in database");
                    return;
                }

                // If still not set, attempt to infer StartedByProviderSubjectId from stored Author/Owner if available in DB (future enhancement)

                // Document ingestion step is only necessary if the review has a document
                if (reviewInstance.ExportedDocumentLink != null)
                {
                    _state.State.Status = ReviewExecutionStatus.Ingesting;
                    await SafeWriteStateAsync();

                    // Get document ingestor grain and start the process
                    var ingestorGrain = GrainFactory.GetGrain<IReviewDocumentIngestorGrain>(this.GetPrimaryKey());
                    var ingestResult = await ingestorGrain.IngestDocumentAsync();

                    if (!ingestResult.IsSuccess)
                    {
                        await HandleFailureAsync("Failed to ingest review document", ingestResult.ErrorMessage ?? "Unknown error");
                        return;
                    }

                    if (ingestResult.Data != null)
                    {
                        await OnDocumentIngestedAsync(ingestResult.Data);
                    }
                }
                else
                {
                    // Skip document ingestion if there's no document, but still need to distribute questions
                    _logger.LogInformation("No document to ingest for review instance {Id}, skipping to question distribution", this.GetPrimaryKey());

                    _state.State.Status = ReviewExecutionStatus.DistributingQuestions;
                    await SafeWriteStateAsync();

                    // Start distributing questions
                    await SendProcessingMessageAsync($"SYSTEM:NoDocumentToIngest");
                    var questionsDistributorGrain = GrainFactory.GetGrain<IReviewQuestionsDistributorGrain>(this.GetPrimaryKey());
                    var distributionResult = await questionsDistributorGrain.DistributeQuestionsAsync();

                    if (!distributionResult.IsSuccess)
                    {
                        await HandleFailureAsync("Failed to distribute review questions", distributionResult.ErrorMessage ?? "Unknown error");
                        return;
                    }

                    await OnQuestionsDistributedAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting review execution for review instance {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed to start review execution", ex.Message);
            }
        }

        public async Task OnDocumentIngestedAsync(ReviewDocumentIngestionResult ingestionResult)
        {
            try
            {
                _logger.LogInformation("Document ingested for review instance {Id} with {QuestionCount} total questions",
                    this.GetPrimaryKey(), ingestionResult.TotalNumberOfQuestions);

                // Update state with document info and total questions
                _state.State.ExportedDocumentLinkId = ingestionResult.ExportedDocumentLinkId;
                _state.State.TotalNumberOfQuestions = ingestionResult.TotalNumberOfQuestions;
                _state.State.Status = ReviewExecutionStatus.DistributingQuestions;
                _state.State.ContentType = ingestionResult.ContentType.ToString();
                await SafeWriteStateAsync();

                // Send processing notification
                await SendProcessingMessageAsync($"SYSTEM:TotalNumberOfQuestions={ingestionResult.TotalNumberOfQuestions}");
                await SendProcessingMessageAsync($"SYSTEM:ContentType={ingestionResult.ContentType}");

                // Start distributing questions
                var questionsDistributorGrain = GrainFactory.GetGrain<IReviewQuestionsDistributorGrain>(this.GetPrimaryKey());
                var distributionResult = await questionsDistributorGrain.DistributeQuestionsAsync();

                if (!distributionResult.IsSuccess)
                {
                    await HandleFailureAsync("Failed to distribute review questions", distributionResult.ErrorMessage ?? "Unknown error");
                    return;
                }

                await OnQuestionsDistributedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling document ingestion for review instance {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed during document ingestion", ex.Message);
            }
        }

        public async Task OnQuestionsDistributedAsync()
        {
            try
            {
                _logger.LogInformation("Questions distributed for review instance {Id}", this.GetPrimaryKey());

                _state.State.Status = ReviewExecutionStatus.AnsweringQuestions;
                await SafeWriteStateAsync();

                // The answerer grain will call back via OnQuestionAnsweredAsync as each question is answered
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling question distribution for review instance {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed during question distribution", ex.Message);
            }
        }

        public async Task OnQuestionAnsweredAsync(Guid questionAnswerId)
        {
            try
            {
                await _stateLock.WaitAsync();
                try
                {
                    _logger.LogInformation("Question answered with answer ID {QuestionAnswerId} for review instance {Id}",
                        questionAnswerId, this.GetPrimaryKey());

                    _state.State.NumberOfQuestionsAnswered++;
                    _state.State.LastUpdatedUtc = DateTime.UtcNow;
                    await _state.WriteStateAsync();
                }
                finally
                {
                    _stateLock.Release();
                }

                // Send notification about question being answered
                await SendProcessingMessageAsync($"SYSTEM:QuestionAnswered={_state.State.NumberOfQuestionsAnswered}");

                // Send notification through notifier grain about answer
                var notifierGrain = GrainFactory.GetGrain<IReviewNotifierGrain>(Guid.Empty);
                await notifierGrain.NotifyReviewQuestionAnsweredAsync(new ReviewQuestionAnsweredNotification(this.GetPrimaryKey())
                {
                    ReviewQuestionAnswerId = questionAnswerId
                });

                // Start sentiment analysis for this answer
                var analyzerGrain = GrainFactory.GetGrain<IReviewAnswerSentimentAnalyzerGrain>(questionAnswerId);
                var analysisResult = await analyzerGrain.AnalyzeSentimentAsync();

                if (!analysisResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to analyze sentiment for answer {AnswerId}, but continuing process: {ErrorMessage}",
                        questionAnswerId, analysisResult.ErrorMessage);
                }

                await OnQuestionAnswerAnalyzedAsync(questionAnswerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling question answer for review instance {Id}", this.GetPrimaryKey());
                // Don't fail the entire process for a single analysis failure
                // Just log and continue
            }
        }

        public async Task OnQuestionAnswerAnalyzedAsync(Guid questionAnswerId)
        {
            try
            {
                bool shouldFinalize = false;

                await _stateLock.WaitAsync();
                try
                {
                    _logger.LogInformation("Question answer analyzed for answer ID {AnswerId} in review instance {Id}",
                        questionAnswerId, this.GetPrimaryKey());

                    _state.State.NumberOfQuestionsAnalyzed++;
                    _state.State.LastUpdatedUtc = DateTime.UtcNow;

                    // Check if all questions have been processed (while under lock)
                    shouldFinalize = _state.State.NumberOfQuestionsAnalyzed >= _state.State.TotalNumberOfQuestions;

                    await _state.WriteStateAsync();
                }
                finally
                {
                    _stateLock.Release();
                }

                // Call finalize outside the lock to avoid lock contention
                if (shouldFinalize)
                {
                    await FinalizeReviewAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling question answer analysis for review instance {Id}", this.GetPrimaryKey());
            }
        }

        private async Task FinalizeReviewAsync()
        {
            _logger.LogInformation("Finalizing review execution for review instance {Id}", this.GetPrimaryKey());

            try
            {
                // Update grain state
                _state.State.Status = ReviewExecutionStatus.Completed;
                await SafeWriteStateAsync();

                // Update the database entity
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var reviewInstance = await dbContext.ReviewInstances.FindAsync(this.GetPrimaryKey());

                if (reviewInstance != null)
                {
                    reviewInstance.Status = ReviewInstanceStatus.Completed;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated review instance status to Completed in database for {Id}", this.GetPrimaryKey());
                }
                else
                {
                    _logger.LogWarning("Could not find review instance {Id} in database to update status", this.GetPrimaryKey());
                }

                // Send completion notification
                await SendProcessingMessageAsync("SYSTEM:ReviewInstanceCompleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing review instance {Id}", this.GetPrimaryKey());
            }
        }

        private async Task HandleFailureAsync(string reason, string details)
        {
            _logger.LogError("Review execution failed for review instance {Id}: {Reason} - {Details}",
                this.GetPrimaryKey(), reason, details);

            try
            {
                // Update grain state
                _state.State.Status = ReviewExecutionStatus.Failed;
                _state.State.FailureReason = reason;
                _state.State.FailureDetails = details;
                await SafeWriteStateAsync();

                // Update the database entity
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                var reviewInstance = await dbContext.ReviewInstances.FindAsync(this.GetPrimaryKey());

                if (reviewInstance != null)
                {
                    reviewInstance.Status = ReviewInstanceStatus.Failed;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated review instance status to Failed in database for {Id}", this.GetPrimaryKey());
                }
                else
                {
                    _logger.LogWarning("Could not find review instance {Id} in database to update status", this.GetPrimaryKey());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating failed status for review instance {Id}", this.GetPrimaryKey());
            }
        }

        private async Task SendProcessingMessageAsync(string message)
        {
            try
            {
                var notifierGrain = GrainFactory.GetGrain<IReviewNotifierGrain>(Guid.Empty);
                await notifierGrain.NotifyProcessingMessageAsync(new BackendProcessingMessageGenerated(this.GetPrimaryKey(), message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send processing message for review instance {Id}", this.GetPrimaryKey());
            }
        }

        private async Task SafeWriteStateAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                _state.State.LastUpdatedUtc = DateTime.UtcNow;
                await _state.WriteStateAsync();
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }
}
