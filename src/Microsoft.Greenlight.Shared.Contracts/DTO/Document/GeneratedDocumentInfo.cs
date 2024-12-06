namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

public class GeneratedDocumentInfo
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? AuthorOid { get; set; }
    public DateTimeOffset Created { get; set; }
    public List<ContentNodeInfo> ContentNodes { get; set; } = new List<ContentNodeInfo>();
}