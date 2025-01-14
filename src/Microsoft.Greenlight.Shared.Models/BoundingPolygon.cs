using System.Text.Json.Serialization;
namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a bounding polygon.
/// </summary>
public class BoundingPolygon : EntityBase
{
    /// <summary>
    /// Value indicating whether the bounding polygon is empty.
    /// </summary>
    public bool IsEmpty { get; set; } = false;

    /// <summary>
    /// X coordinate of the bounding polygon.
    /// </summary>
    public decimal X { get; set; }

    /// <summary>
    /// Y coordinate of the bounding polygon.
    /// </summary>
    public decimal Y { get; set; }

    /// <summary>
    /// Unique ID of the bounding region.
    /// </summary>
    public Guid BoundingRegionId { get; set; }

    /// <summary>
    /// Bounding region associated with the bounding polygon.
    /// </summary>
    [JsonIgnore]
    public BoundingRegion? BoundingRegion { get; set; }
}
