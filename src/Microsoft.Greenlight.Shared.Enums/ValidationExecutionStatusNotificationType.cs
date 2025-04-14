namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Signifies the type of notification for validation execution status.
    /// 
    /// </summary>
    public enum ValidationExecutionStatusNotificationType
    {
        /// <summary>
        /// Validation execution has started
        /// </summary>
        ValidationExecutionStarted = 100,

        /// <summary>
        /// Execution of a specific step has started
        /// </summary>
        ValidationStepStarted = 200,

        /// <summary>
        /// Execution of a specific step has completed
        /// </summary>
        ValidationStepCompleted = 300,

        /// <summary>
        /// A validation step has resulted in a change request to a content node
        /// </summary>
        ValidationStepContentChangeRequested = 350,
        
        /// <summary>
        /// Execution of a step has failed
        /// </summary>
        ValidationStepFailed = 399,

        /// <summary>
        /// A new validation execution has been created and it resulted in abandoning previous unapplied validations
        /// </summary>
        PreviousUnappliedValidationsAbandoned = 400,

        /// <summary>
        /// The validation execution has completed
        /// </summary>
        ValidationExecutionCompleted = 999,
        
        /// <summary>
        /// The validation Execution has failed
        /// </summary>
        ValidationExecutionFailed = 850
    }
}