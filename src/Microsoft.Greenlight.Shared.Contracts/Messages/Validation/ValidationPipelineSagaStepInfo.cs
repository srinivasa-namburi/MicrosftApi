using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation
{
    public class ValidationPipelineSagaStepInfo
    {
        public Guid StepId { get; set; }
        public int Order { get; set; }
        public ValidationPipelineExecutionType ExecutionType { get; set; }
    }
}