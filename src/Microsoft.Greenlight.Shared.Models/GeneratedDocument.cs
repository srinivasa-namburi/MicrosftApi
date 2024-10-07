namespace Microsoft.Greenlight.Shared.Models;

public class GeneratedDocument : EntityBase
{

    public string Title { get; set; }
    public DateTime GeneratedDate { get; set; }
    public Guid RequestingAuthorOid { get; set; }
    public List<ContentNode> ContentNodes { get; set; } = new List<ContentNode>();
    public string? DocumentProcess { get; set; }

    public Guid? MetadataId { get; set; }
    public DocumentMetadata? Metadata { get; set; } = new DocumentMetadata();

    public List<ExportedDocumentLink> ExportedDocumentLinks { get; set; } = new List<ExportedDocumentLink>();
}
