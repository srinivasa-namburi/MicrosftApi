using Microsoft.Greenlight.Shared.Contracts.Components;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models
{
    /// <summary>
    /// Tracks versions of a content node.
    /// </summary>
    public class ContentNodeVersionTracker : EntityBase
    {
        /// <summary>
        /// The content node ID this tracker is associated with.
        /// </summary>
        public Guid ContentNodeId { get; set; }

        /// <summary>
        /// The content node this tracker is associated with.
        /// </summary>
        [JsonIgnore]
        public virtual ContentNode? ContentNode { get; set; }

        /// <summary>
        /// JSON-encoded list of content node versions.
        /// </summary>
        public string ContentNodeVersionsJson { get; set; } = "[]";

        /// <summary>
        /// List of content node versions (deserialized from ContentNodeVersionsJson).
        /// </summary>
        [NotMapped]
        public List<ContentNodeVersion> ContentNodeVersions
        {
            get => string.IsNullOrEmpty(ContentNodeVersionsJson)
                ? []
                : JsonSerializer.Deserialize<List<ContentNodeVersion>>(ContentNodeVersionsJson) ?? [];
            set => ContentNodeVersionsJson = JsonSerializer.Serialize(value);
        }

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
