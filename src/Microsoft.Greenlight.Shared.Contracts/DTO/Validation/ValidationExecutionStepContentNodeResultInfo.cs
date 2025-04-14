using Microsoft.Greenlight.Shared.Enums;
using Orleans;
using System.ComponentModel.DataAnnotations.Schema;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Validation
{
    /// <summary>
    /// Contract holding content node results for a validation pipeline execution step.
    /// </summary>
    [GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
    public class ValidationExecutionStepContentNodeResultInfo
    {
        /// <summary>
        /// Valid
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// The ID of the validation pipeline execution step result that this content node result is associated with.
        /// Nullable because the content node result is created before the step result is created.
        /// </summary>
        public Guid? ValidationPipelineExecutionStepResultId { get; set; }
        
        /// <summary>
        /// The ID of the validation pipeline execution step that this content node result is associated with.
        /// </summary>
        public Guid? ValidationPipelineExecutionStepId { get; set; }

        /// <summary>
        /// The ID of the original content node prior to validation.
        /// </summary>
        public Guid OriginalContentNodeId { get; set; }

        /// <summary>
        /// The ID of the resultant content node after validation.
        /// </summary>
        public Guid ResultantContentNodeId { get; set; }

        /// <summary>
        /// If changes were requested for this content node in a validation step (computed)
        /// </summary>
        [NotMapped]
        public bool ChangesRequested => !OriginalContentNodeId.Equals(ResultantContentNodeId) && ContentNodeResultIsValid;

        [NotMapped] 
        private bool ContentNodeResultIsValid => OriginalContentNodeId != Guid.Empty && ResultantContentNodeId != Guid.Empty;

        public ValidationContentNodeApplicationStatus? ApplicationStatus { get; set; }
    }
}