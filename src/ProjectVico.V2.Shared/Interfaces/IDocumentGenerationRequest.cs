namespace ProjectVico.V2.Shared.Interfaces;

public interface IDocumentGenerationRequest
{
    string DocumentProcessName { get; set; }
    string DocumentTitle { get; set; }
    string? AuthorOid { get; set; }
    string? MetadataModelName { get; }
    string? DocumentGenerationRequestFullTypeName { get; }
    Guid Id { get; set; }
}