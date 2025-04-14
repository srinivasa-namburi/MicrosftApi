namespace Microsoft.Greenlight.Shared.Enums
{
    /// <summary>
    /// Status for an individual recommendation
    /// </summary>
    public enum ValidationContentNodeApplicationStatus
    {
        /// <summary>
        /// There are changes recommended - but they have not been applied yet
        /// </summary>
        Unapplied = 0,

        /// <summary>
        /// No changes recommended for this content node
        /// </summary>
        NoChangesRecommended = 50,

        /// <summary>
        /// Changes have been accepted and applied to the original content node
        /// </summary>
        Accepted = 100,

        /// <summary>
        /// Changes have been rejected and not applied to the original content node
        /// </summary>
        Rejected = 200
    }
}