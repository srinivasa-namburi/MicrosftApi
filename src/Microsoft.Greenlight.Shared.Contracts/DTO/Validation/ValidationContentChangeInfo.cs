using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Validation
{
    /// <summary>
    /// Information about a recommended content change from validation
    /// </summary>
    public class ValidationContentChangeInfo
    {
        /// <summary>
        /// The ID of the ContentNodeResult that is being validated. Used to bind back Application Status changes.
        /// </summary>
        public required Guid OriginalValidationExecutionStepContentNodeResultId { get; set; }
        /// <summary>
        /// The original content node ID
        /// </summary>
        public required Guid OriginalContentNodeId { get; set; }
        
        /// <summary>
        /// The resultant content node ID with suggested changes
        /// </summary>
        public required Guid ResultantContentNodeId { get; set; }
        
        /// <summary>
        /// The original text content
        /// </summary>
        public string? OriginalText { get; set; }
        
        /// <summary>
        /// The suggested text content
        /// </summary>
        public string? SuggestedText { get; set; }
        
        /// <summary>
        /// The ID of the parent content node (section heading)
        /// </summary>
        public Guid? ParentContentNodeId { get; set; }

        public ValidationContentNodeApplicationStatus? ApplicationStatus { get; set; }
    }
}