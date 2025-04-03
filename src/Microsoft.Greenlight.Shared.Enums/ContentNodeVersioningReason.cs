namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Reasons for creating a new content node version.
    /// </summary>
    public enum ContentNodeVersioningReason
    {
        /// <summary>
        /// Manual edit by a user.
        /// </summary>
        ManualEdit,

        /// <summary>
        /// Result of a validation process.
        /// </summary>
        ValidationRun,

        /// <summary>
        /// AI-assisted edit.
        /// </summary>
        AiEdit,

        /// <summary>
        /// System modification.
        /// </summary>
        System
    }
}