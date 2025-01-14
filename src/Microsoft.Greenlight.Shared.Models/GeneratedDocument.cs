namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a generated document with various properties and associated metadata.
/// </summary>
public class GeneratedDocument : EntityBase
{
    /// <summary>
    /// Title of the generated document.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Date when the document was generated.
    /// </summary>
    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// Unique identifier of the author who requested the document.
    /// </summary>
    public Guid RequestingAuthorOid { get; set; }

    /// <summary>
    /// List of content nodes associated with the document.
    /// </summary>
    public List<ContentNode> ContentNodes { get; set; } = [];

    /// <summary>
    /// Document process associated with the document.
    /// </summary>
    public string? DocumentProcess { get; set; }

    /// <summary>
    /// Unique identifier for the metadata associated with the document.
    /// </summary>
    public Guid? MetadataId { get; set; }

    /// <summary>
    /// Metadata associated with the document.
    /// </summary>
    public DocumentMetadata? Metadata { get; set; } = new DocumentMetadata();

    /// <summary>
    /// List of links to exported versions of the document.
    /// </summary>
    public List<ExportedDocumentLink> ExportedDocumentLinks { get; set; } = [];
}
