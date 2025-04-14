namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Represents the application status of a validation pipeline execution.
    /// </summary>
    public enum ValidationPipelineExecutionApplicationStatus
    {
        /// <summary>
        /// The validation execution results have not yet been applied to the document.
        /// </summary>
        Unapplied = 0,

        /// <summary>
        /// Validation application is in progress and some changes have been applied
        /// </summary>
        PartiallyApplied = 50,
        
        /// <summary>
        /// The validation execution results have been abandoned and will not be applied.
        /// </summary>
        Abandoned = 100,
        
        /// <summary>
        /// The validation execution results have been applied to the document.
        /// </summary>
        Applied = 200
    }
}