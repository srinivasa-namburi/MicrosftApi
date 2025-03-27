using Microsoft.Greenlight.Shared.Enums;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    /// <summary>
    /// Step associated with a DocumentProcessValidationPipeline
    /// </summary>
    public class DocumentProcessValidationPipelineStep : EntityBase
    {
        /// <summary>
        /// The pipeline that this step is associated with
        /// </summary>
        public Guid DocumentProcessValidationPipelineId { get; set; }

        /// <summary>
        /// The pipeline that this step is associated with
        /// </summary>
        [JsonIgnore]
        public DocumentProcessValidationPipeline? DocumentProcessValidationPipeline { get; set; }

        /// <summary>
        /// The order of the step in the pipeline. Steps with the same order can run in random order.
        /// </summary>
        public int Order = 0;

        /// <summary>
        /// The type of validation that will be run for this step
        /// </summary>
        public ValidationPipelineExecutionType PipelineExecutionType { get; set; }
    }
}