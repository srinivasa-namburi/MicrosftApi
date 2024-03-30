using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models.Classification;

namespace ProjectVico.V2.Shared.Models;

public class IngestedDocument : EntityBase
{
    public string FileName { get; set; }
    public string? FileHash { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public string? DocumentProcess { get; set; }
    public IngestionState IngestionState { get; set; } = IngestionState.Uploaded;
    
    public string? ClassificationShortCode { get; set; }
    public DocumentClassificationType? ClassificationType { get; set; }

    
    public DateTime IngestedDate { get; set; }
    public List<ContentNode> ContentNodes { get; set; } = new List<ContentNode>();
    public List<Table> Tables { get; set; } = new List<Table>();
}