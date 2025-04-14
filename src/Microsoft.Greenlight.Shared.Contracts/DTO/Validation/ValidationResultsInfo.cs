using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Validation
{
    /// <summary>
    /// Validation results with recommended changes
    /// </summary>
    public class ValidationResultsInfo
    {
        /// <summary>
        /// The validation execution ID
        /// </summary>
        public required Guid ValidationExecutionId { get; set; }
        
        /// <summary>
        /// The overall application status of the validation
        /// </summary>
        public ValidationPipelineExecutionApplicationStatus ApplicationStatus { get; set; }
        
        /// <summary>
        /// List of recommended content changes from validation
        /// </summary>
        public List<ValidationContentChangeInfo> ContentChanges { get; set; } = [];
        
        /// <summary>
        /// When the validation was completed
        /// </summary>
        public DateTimeOffset CompletedAt { get; set; }
    }
}