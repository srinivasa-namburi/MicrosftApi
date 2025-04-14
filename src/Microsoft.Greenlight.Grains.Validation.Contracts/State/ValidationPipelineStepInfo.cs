using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Grains.Validation.Contracts.State
{
    [Serializable]
    public class ValidationPipelineStepInfo
    {
        public Guid StepId { get; set; }
        public int Order { get; set; }
        public ValidationPipelineExecutionType ExecutionType { get; set; }
    }
}