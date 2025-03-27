namespace Microsoft.Greenlight.Shared.Enums
{
    public enum ValidationPipelineExecutionStepStatus
    {
        /// <summary>
        /// Validation Execution Step has not been started
        /// </summary>
        NotStarted = 0,
        /// <summary>
        /// Validation Execution Step is in progress
        /// </summary>
        InProgress = 100,
        /// <summary>
        /// Validation Execution Step has failed
        /// </summary>
        Failed = 800,
        /// <summary>
        /// Validation Execution Step is complete
        /// </summary>
        Done = 999
    }
}