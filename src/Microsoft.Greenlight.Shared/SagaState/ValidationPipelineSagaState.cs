using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation;

namespace Microsoft.Greenlight.Shared.SagaState
{
    public class ValidationPipelineSagaState : SagaStateMachineInstance
    {
        /// <summary>
        /// CorrelationId is ValidationPipelineExecutionId from the ValidationPipelineExecution in question
        /// </summary>
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; }
        
        public Guid GeneratedDocumentId { get; set; }
        
        // For tracking step execution
        public List<ValidationPipelineSagaStepInfo> OrderedSteps { get; set; } = new();
        public int CurrentStepIndex { get; set; }
        
    }
}