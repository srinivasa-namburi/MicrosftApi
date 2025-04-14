using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents information about a validation pipeline step.
    /// </summary>
    public class DocumentProcessValidationPipelineStepInfo
    {
        /// <summary>
        /// Unique identifier of the validation pipeline step.
        /// </summary>
        public Guid Id { get; set; }
    
        /// <summary>
        /// The order of the step in the pipeline.
        /// </summary>
        public int Order { get; set; }
    
        /// <summary>
        /// The type of validation execution for this step.
        /// </summary>
        public ValidationPipelineExecutionType PipelineExecutionType { get; set; }
    }
}