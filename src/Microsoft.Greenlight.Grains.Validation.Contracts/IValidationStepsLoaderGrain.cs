using Microsoft.Greenlight.Grains.Validation.Contracts.Models;
using Microsoft.Greenlight.Grains.Validation.Contracts.State;
using Orleans;

namespace Microsoft.Greenlight.Grains.Validation.Contracts
{
    public interface IValidationStepsLoaderGrain : IGrainWithGuidKey
    {
        Task<ValidationStepResult<List<ValidationPipelineStepInfo>>> LoadValidationStepsAsync(
            Guid validationExecutionId, Guid generatedDocumentId);
    }
}