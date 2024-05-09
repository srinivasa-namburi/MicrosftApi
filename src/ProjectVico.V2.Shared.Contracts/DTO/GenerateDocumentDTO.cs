namespace ProjectVico.V2.Shared.Contracts.DTO;

public record GenerateDocumentDTO
{
    public string DocumentProcessName { get; set; }
    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }
    public string? MetadataModelName { get; }
    public string? DocumentGenerationRequestFullTypeName { get; }
    public string? RequestAsJson { get; set; }
    public Guid Id { get; set; }
}