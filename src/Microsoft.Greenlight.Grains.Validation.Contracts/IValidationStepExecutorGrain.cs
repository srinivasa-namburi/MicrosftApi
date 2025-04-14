using Microsoft.Greenlight.Grains.Validation.Contracts.Models;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Validation.Contracts
{
    public interface IValidationStepExecutorGrain : IGrainWithGuidKey
    {
        Task<ValidationStepResult> ExecuteStepAsync(
            Guid validationExecutionId, ValidationPipelineExecutionType executionType);
    }
}