namespace ProjectVico.V2.Shared.Models;

public class GeneratedDocument : EntityBase
{

    public string Title { get; set; }
    public DateTime GeneratedDate { get; set; }
    public Guid RequestingAuthorOid { get; set; }
    public List<ContentNode> ContentNodes { get; set; } = new List<ContentNode>();
}