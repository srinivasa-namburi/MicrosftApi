using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document
{
    /// <summary>
    /// Represents information about a content node version tracker.
    /// </summary>
    public class ContentNodeVersionTrackerInfo
    {
        /// <summary>
        /// Unique identifier of the content node version tracker.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The content node ID this tracker is associated with.
        /// </summary>
        public Guid ContentNodeId { get; set; }

        /// <summary>
        /// List of content node versions (deserialized from ContentNodeVersionsJson).
        /// </summary>
        public List<ContentNodeVersion> ContentNodeVersions { get; set; } = [];

        /// <summary>
        /// Current version number of the content node.
        /// </summary>
        public int CurrentVersion { get; set; } = 1;
        
        /// <summary>
        /// Type of the content node this tracker is tracking.
        /// Must be BodyText, as only BodyText nodes are allowed to be versioned.
        /// </summary>
        public ContentNodeType ContentNodeType { get; set; }
    }
}