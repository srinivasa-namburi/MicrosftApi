using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    /// <summary>
    /// Definition of validation steps that are run for a particular document process
    /// </summary>
    public class DocumentProcessValidationPipeline : EntityBase
    {
        /// <summary>
        /// The document process that this pipeline is associated with
        /// </summary>
        public required Guid DocumentProcessId { get; set; }

        /// <summary>
        /// The document process that this pipeline is associated with
        /// </summary> 
        [JsonIgnore]
        public DynamicDocumentProcessDefinition? DocumentProcess { get; set; }

        /// <summary>
        /// The validation steps and their order that are run for this pipeline
        /// </summary>
        public List<DocumentProcessValidationPipelineStep> ValidationPipelineSteps { get; set; } = [];

        /// <summary>
        /// The validation executions that have been run for this pipeline
        /// </summary>
        public List<ValidationPipelineExecution> ValidationPipelineExecutions { get; set; } = [];
    }
}