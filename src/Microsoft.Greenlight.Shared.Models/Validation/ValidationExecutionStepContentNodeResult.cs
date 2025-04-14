using Microsoft.Greenlight.Shared.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.Validation
{
    /// <summary>
    /// Holds content node results for a validation pipeline execution step.
    /// There is one of these for each BodyText content node in the processed document.
    /// </summary>
    public class ValidationExecutionStepContentNodeResult : EntityBase
    {
        /// <summary>
        /// The ID of the validation pipeline execution step result that this content node result is associated with.
        /// Nullable because the content node result is created before the step result is created.
        /// </summary>
        public Guid? ValidationPipelineExecutionStepResultId { get; set; }

        /// <summary>
        /// The validation pipeline execution step result that this content node result is associated with.
        /// </summary>
        [JsonIgnore]
        public ValidationPipelineExecutionStepResult? ValidationPipelineExecutionStepResult { get; set; }

        /// <summary>
        /// The ID of the validation pipeline execution step that this content node result is associated with.
        /// </summary>
        public Guid? ValidationPipelineExecutionStepId { get; set; }

        /// <summary>
        /// The validation pipeline execution step that this content node result is associated with.
        /// </summary>
        [JsonIgnore]
        public ValidationPipelineExecutionStep? ValidationPipelineExecutionStep { get; set; }

        /// <summary>
        /// The ID of the original content node prior to validation.
        /// </summary>
        public Guid OriginalContentNodeId { get; set; }

        /// <summary>
        /// The original content node prior to validation.
        /// </summary>
        [JsonIgnore]
        public ContentNode? OriginalContentNode { get; set; }

        /// <summary>
        /// The ID of the resultant content node after validation.
        /// </summary>
        public Guid ResultantContentNodeId { get; set; }

        /// <summary>
        /// The resultant content node after validation.
        /// </summary>
        [JsonIgnore]
        public ContentNode? ResultantContentNode { get; set; }

        /// <summary>
        /// Status of application of this content node result
        /// </summary>
        public ValidationContentNodeApplicationStatus? ApplicationStatus { get; set; } = ValidationContentNodeApplicationStatus.NoChangesRecommended;
        

        /// <summary>
        /// If changes were requested for this content node in a validation step (computed)
        /// </summary>
        [NotMapped]
        public bool ChangesRequested => (!OriginalContentNodeId.Equals(ResultantContentNodeId) && ContentNodeResultIsValid);

        [NotMapped]
        private bool ContentNodeResultIsValid => OriginalContentNodeId != Guid.Empty && ResultantContentNodeId != Guid.Empty;

    }
}
