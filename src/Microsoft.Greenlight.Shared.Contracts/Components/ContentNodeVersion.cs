using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Components
{
    /// <summary>
    /// Represents a specific version of a content node.
    /// This is a component and not stored in a separate table.
    /// </summary>
    public class ContentNodeVersion
    {
        /// <summary>
        /// Unique identifier for this version.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Version number.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// UTC timestamp when this version was created.
        /// </summary>
        public DateTime VersionTimeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional comment about this version.
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Serialized content node text.
        /// Since only BodyText nodes are versionable, we only need to store the text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The reason this version was created.
        /// </summary>
        public ContentNodeVersioningReason VersioningReason { get; set; } = ContentNodeVersioningReason.ManualEdit;
    }
}