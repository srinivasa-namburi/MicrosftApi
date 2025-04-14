using Microsoft.Greenlight.Grains.Validation.Contracts.State;
using Orleans;

namespace Microsoft.Greenlight.Grains.Validation.Contracts
{
    public interface IValidationPipelineOrchestrationGrain : IGrainWithGuidKey
    {
        Task<ValidationPipelineState> GetStateAsync();
        Task StartValidationPipelineAsync(Guid generatedDocumentId);
        Task OnValidationStepsLoadedAsync(List<ValidationPipelineStepInfo> orderedSteps);
        Task OnValidationStepCompletedAsync(Guid stepId);
        Task OnValidationStepFailedAsync(Guid stepId, string errorMessage);
    }
}