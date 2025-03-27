using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    /// <summary>
    /// A single step in the validation process.
    /// </summary>
    public class ValidationPipelineExecutionStep : EntityBase
    {
        /// <summary>
        /// Gets or sets the ID of the validation execution.
        /// </summary>
        public required Guid ValidationPipelineExecutionId { get; set; }

        /// <summary>
        /// Gets or sets the validation execution associated with this step.
        /// </summary>
        [JsonIgnore]
        public ValidationPipelineExecution? ValidationPipelineExecution { get; set; }

        /// <summary>
        /// Gets or sets the type of the validation execution.
        /// </summary>
        public required ValidationPipelineExecutionType PipelineExecutionType { get; set; }

        /// <summary>
        /// Gets or sets the status of the validation execution step.
        /// </summary>
        public ValidationPipelineExecutionStepStatus PipelineExecutionStepStatus { get; set; } = ValidationPipelineExecutionStepStatus.NotStarted;

        /// <summary>
        /// Gets or sets the order of the validation execution step.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets the rendered full document text for the step.
        /// </summary>
        public string? RenderedFullDocumentTextForStep { get; set; }
        
        /// <summary>
        /// Direct link to previous and resulting content nodes for this step.
        /// </summary>
        public List<ValidationExecutionStepContentNodeResult> ValidationExecutionStepContentNodeResults { get; set; } = [];

        /// <summary>
        /// Gets or sets the ID of the validation execution step result.
        /// </summary>
        public Guid? ValidationPipelineExecutionStepResultId { get; set; }

        /// <summary>
        /// Gets or sets the validation execution step result associated with this step.
        /// </summary>
        [JsonIgnore]
        public ValidationPipelineExecutionStepResult? ValidationPipelineExecutionStepResult { get; set; }
    }

}


