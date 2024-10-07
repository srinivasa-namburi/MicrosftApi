using System.Text.Json.Serialization;
namespace Microsoft.Greenlight.Shared.Models;

public class BoundingPolygon : EntityBase
{
    public bool IsEmpty { get; set; } = false;
    public decimal X { get; set; }
    public decimal Y { get; set; }

    public Guid BoundingRegionId { get; set; }
    [JsonIgnore]
    public BoundingRegion BoundingRegion { get; set; }
}
