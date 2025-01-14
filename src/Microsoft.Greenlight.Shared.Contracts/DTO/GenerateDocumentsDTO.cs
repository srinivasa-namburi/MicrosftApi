namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a data transfer object for generating documents.
/// </summary>
public record GenerateDocumentsDTO
{
    /// <summary>
    /// The list of documents to be generated.
    /// </summary>
    public GenerateDocumentDTO[] Documents { get; set; }
}

/// <summary>
/// Represents a request for document generation.
/// </summary>
public class DocumentGenerationRequest
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
    public string AuthorOid { get; set; }

    /// <summary>
    /// ID of the document generation request.
    /// </summary>
    public string Id { get; set; }
}
