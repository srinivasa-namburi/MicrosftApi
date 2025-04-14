namespace Microsoft.Greenlight.Shared.Contracts.DTO
{
    /// <summary>
    /// Represents information about a validation pipeline.
    /// </summary>
    public class DocumentProcessValidationPipelineInfo
    {
        /// <summary>
        /// Unique identifier of the validation pipeline.
        /// </summary>
        public Guid Id { get; set; }
    
        /// <summary>
        /// The document process ID that this pipeline is associated with.
        /// </summary>
        public Guid DocumentProcessId { get; set; }
        
        /// <summary>
        /// Indicates whether validation pipeline steps should run automatically after document generation
        /// </summary>
        public bool RunValidationAutomatically { get; set; } = false;
    
        /// <summary>
        /// The validation steps and their order that are run for this pipeline.
        /// </summary>
        public List<DocumentProcessValidationPipelineStepInfo> ValidationPipelineSteps { get; set; } = [];
    }
}