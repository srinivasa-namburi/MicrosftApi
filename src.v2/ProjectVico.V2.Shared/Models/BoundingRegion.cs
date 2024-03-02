namespace ProjectVico.V2.Shared.Models;

public class BoundingRegion : EntityBase
{
   
  
    public int Page { get; set; }
    public List<BoundingPolygon>? BoundingPolygons { get; set; } = new List<BoundingPolygon>();
    public ContentNode? ContentNode { get; set; }
    public Guid? ContentNodeId { get; set; }
    public Table? Table { get; set; }
    public Guid? TableId { get; set; }



}