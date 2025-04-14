using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    /// <summary>
    /// Tracks execution of a validation run
    /// </summary>
    public class ValidationPipelineExecution : EntityBase
    {
        /// <summary>
        /// The steps that will be or has been executed for this Validation Execution
        /// </summary>
        public List<ValidationPipelineExecutionStep> ExecutionSteps { get; set; } = [];

        /// <summary>
        /// The ID of the document process validation pipeline that this validation execution is associated with
        /// </summary>
        public required Guid DocumentProcessValidationPipelineId { get; set; }

        /// <summary>
        /// The document process validation pipeline that this validation execution is associated with
        /// </summary>
        [JsonIgnore]
        public DocumentProcessValidationPipeline? DocumentProcessValidationPipeline { get; set; }

        /// <summary>
        /// Document ID that this validation execution is associated with
        /// </summary>
        public required Guid GeneratedDocumentId { get; set; }

        /// <summary>
        /// Document that this validation execution is associated with
        /// </summary>
        [JsonIgnore] 
        public GeneratedDocument? GeneratedDocument { get; set; }

        /// <summary>
        /// The status of applying the validation execution results to the document
        /// </summary>
        public ValidationPipelineExecutionApplicationStatus ApplicationStatus { get; set; } = ValidationPipelineExecutionApplicationStatus.Unapplied;

    }
}