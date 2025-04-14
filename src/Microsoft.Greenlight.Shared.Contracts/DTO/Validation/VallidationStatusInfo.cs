namespace Microsoft.Greenlight.Shared.Contracts.DTO.Validation
{
    /// <summary>
    /// DTO for validation status information
    /// </summary>
    public class ValidationStatusInfo
    {
        /// <summary>
        /// The document ID being validated
        /// </summary>
        public required Guid DocumentId { get; set; }
        
        /// <summary>
        /// The validation execution ID
        /// </summary>
        public required Guid ValidationId { get; set; }
        
        /// <summary>
        /// When the validation was started
        /// </summary>
        public DateTimeOffset Started { get; set; }
        
        /// <summary>
        /// Steps in the validation pipeline
        /// </summary>
        public required List<ValidationStepStatusInfo> Steps { get; set; }
    }
}
