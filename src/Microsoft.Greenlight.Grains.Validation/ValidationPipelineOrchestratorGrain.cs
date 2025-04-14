using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Grains.Validation.Contracts.State;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Validation
{
    /// <summary>
    /// Orchestrates the validation pipeline process, replacing the MassTransit-based ValidationPipelineSaga
    /// </summary>
    [Reentrant]
    public class ValidationPipelineOrchestrationGrain : Grain, IValidationPipelineOrchestrationGrain
    {
        private readonly IPersistentState<ValidationPipelineState> _state;
        private readonly ILogger<ValidationPipelineOrchestrationGrain> _logger;
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        public ValidationPipelineOrchestrationGrain(
            [PersistentState("validationPipeline")]
            IPersistentState<ValidationPipelineState> state,
            ILogger<ValidationPipelineOrchestrationGrain> logger)
        {
            _state = state;
            _logger = logger;
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

        public async Task<ValidationPipelineState> GetStateAsync()
        {
            return _state.State;
        }

        public async Task StartValidationPipelineAsync(Guid generatedDocumentId)
        {
            try
            {
                _logger.LogInformation("Starting validation pipeline process for execution {Id}", this.GetPrimaryKey());

                // Store initial state
                _state.State.GeneratedDocumentId = generatedDocumentId;
                _state.State.Status = ValidationPipelineStatus.Loading;
                _state.State.CurrentStepIndex = -1;
                await SafeWriteStateAsync();

                // Get validation steps loader grain and start the process
                var stepsLoaderGrain = GrainFactory.GetGrain<IValidationStepsLoaderGrain>(this.GetPrimaryKey());

                var stepsResult = await stepsLoaderGrain.LoadValidationStepsAsync(_state.State.Id, generatedDocumentId);

                if (!stepsResult.IsSuccess)
                {
                    await HandleFailureAsync("Failed to load validation steps", stepsResult.ErrorMessage ?? "Unknown error");
                    return;
                }

                await OnValidationStepsLoadedAsync(stepsResult.Data!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting validation pipeline for execution {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed to start validation pipeline", ex.Message);
            }
        }


        public async Task OnValidationStepsLoadedAsync(List<ValidationPipelineStepInfo> orderedSteps)
        {
            try
            {
                _logger.LogInformation("Loaded {Count} validation steps for execution {Id}",
                    orderedSteps.Count, this.GetPrimaryKey());

                _state.State.OrderedSteps = orderedSteps;
                _state.State.Status = ValidationPipelineStatus.Executing;
                _state.State.CurrentStepIndex = 0;
                await SafeWriteStateAsync();

                // Execute the first step
                if (orderedSteps.Count > 0)
                {
                    await ExecuteCurrentStepAsync();
                }
                else
                {
                    await FinalizeValidationPipelineAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling validation steps loaded for execution {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed while processing loaded validation steps", ex.Message);
            }
        }

        public async Task OnValidationStepCompletedAsync(Guid stepId)
        {
            try
            {
                _logger.LogInformation("Step completed: {StepId} for execution {Id}",
                    stepId, this.GetPrimaryKey());

                // Move to the next step
                _state.State.CurrentStepIndex++;
                await SafeWriteStateAsync();

                // Check if there are more steps
                if (_state.State.CurrentStepIndex < _state.State.OrderedSteps.Count)
                {
                    // Execute the next step
                    await ExecuteCurrentStepAsync();
                }
                else
                {
                    // All steps completed, finalize the pipeline
                    await FinalizeValidationPipelineAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling validation step completion for execution {Id}", this.GetPrimaryKey());
                await HandleFailureAsync("Failed during step completion", ex.Message);
            }
        }

        public async Task OnValidationStepFailedAsync(Guid stepId, string errorMessage)
        {
            await HandleFailureAsync($"Step {stepId} failed", errorMessage);
        }

        private async Task ExecuteCurrentStepAsync()
        {
            var currentStep = _state.State.OrderedSteps[_state.State.CurrentStepIndex];
            _logger.LogInformation("Executing step {StepId} (order {Order}) for execution {Id}",
                currentStep.StepId, currentStep.Order, this.GetPrimaryKey());

            var stepExecutorGrain = GrainFactory.GetGrain<IValidationStepExecutorGrain>(currentStep.StepId);

            var executionResult = await stepExecutorGrain.ExecuteStepAsync(this.GetPrimaryKey(), currentStep.ExecutionType);

            if (executionResult.IsSuccess)
            {
                await OnValidationStepCompletedAsync(currentStep.StepId);
            }
            else
            {
                await OnValidationStepFailedAsync(currentStep.StepId, executionResult.ErrorMessage ?? "Unknown error");
            }
        }

        private async Task FinalizeValidationPipelineAsync()
        {
            _logger.LogInformation("Finalizing validation pipeline for execution {Id}", this.GetPrimaryKey());

            _state.State.Status = ValidationPipelineStatus.Completed;
            await SafeWriteStateAsync();

            // Publish completion notification through notifier grain
            var notifierGrain = GrainFactory.GetGrain<IValidationNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyValidationPipelineCompletedAsync(
                this.GetPrimaryKey(),
                _state.State.GeneratedDocumentId);
        }

        private async Task HandleFailureAsync(string reason, string details)
        {
            _logger.LogError("Validation pipeline failed for execution {Id}: {Reason} - {Details}",
                this.GetPrimaryKey(), reason, details);

            _state.State.Status = ValidationPipelineStatus.Failed;
            _state.State.FailureReason = reason;
            _state.State.FailureDetails = details;
            await SafeWriteStateAsync();

            // Publish failure notification
            var notifierGrain = GrainFactory.GetGrain<IValidationNotifierGrain>(Guid.Empty);
            await notifierGrain.NotifyValidationPipelineFailedAsync(
                this.GetPrimaryKey(),
                _state.State.GeneratedDocumentId,
                $"{reason}: {details}");
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
