namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Data Transfer Object for generating a document.
/// </summary>
public record GenerateDocumentDTO
{
    /// <summary>
    /// Name of the document process.
    /// </summary>
    public string DocumentProcessName { get; set; }

    /// <summary>
    /// Title of the document.
    /// </summary>
    public string DocumentTitle { get; set; }

    /// <summary>
    /// OID of the author.
    /// </summary>
    public string? AuthorOid { get; set; }

    /// <summary>
    /// Gets the name of the metadata model.
    /// </summary>
    public string? MetadataModelName { get; }

    /// <summary>
    /// Gets the full type name of the document generation request.
    /// </summary>
    public string? DocumentGenerationRequestFullTypeName { get; }

    /// <summary>
    /// Request in JSON format.
    /// </summary>
    public string? RequestAsJson { get; set; }

    /// <summary>
    /// Unique identifier of the data transfer object.
    /// </summary>
    public Guid Id { get; set; }
}
