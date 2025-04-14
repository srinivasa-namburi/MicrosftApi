namespace Microsoft.Greenlight.Shared.Contracts.DTO.Validation
{
    /// <summary>
    /// Status information for a validation step
    /// </summary>
    public class ValidationStepStatusInfo
    {
        /// <summary>
        /// The step ID
        /// </summary>
        public required Guid StepId { get; set; }
        
        /// <summary>
        /// The order of the step in the pipeline
        /// </summary>
        public int Order { get; set; }
        
        /// <summary>
        /// The type of execution for this step
        /// </summary>
        public required string ExecutionType { get; set; }
        
        /// <summary>
        /// The current status of the step
        /// </summary>
        public required string Status { get; set; }
    }
}