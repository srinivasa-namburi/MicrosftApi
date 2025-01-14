using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a bounding region in a document.
/// </summary>
public class BoundingRegion : EntityBase
{
    /// <summary>
    /// Page number where the bounding region is located.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// List of bounding polygons associated with the bounding region.
    /// </summary>
    public List<BoundingPolygon>? BoundingPolygons { get; set; } = [];

    /// <summary>
    /// Content node associated with the bounding region.
    /// </summary>
    [JsonIgnore]
    public ContentNode? ContentNode { get; set; }

    /// <summary>
    /// Unique content node ID associated with the bounding region.
    /// </summary>
    public Guid? ContentNodeId { get; set; }

    /// <summary>
    /// Table associated with the bounding region.
    /// </summary>
    [JsonIgnore]
    public Table? Table { get; set; }

    /// <summary>
    /// Unique ID of the table associated with the bounding region.
    /// </summary>
    public Guid? TableId { get; set; }
}
