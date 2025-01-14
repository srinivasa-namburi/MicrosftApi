namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a generated document.
/// </summary>
public class GeneratedDocumentInfo
{
    /// <summary>
    /// Unique identifier of the document.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title of the document.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Name of the document.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier of the author.
    /// </summary>
    public Guid? AuthorOid { get; set; }

    /// <summary>
    /// Creation date and time of the document.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// List of content nodes in the document.
    /// </summary>
    public List<ContentNodeInfo> ContentNodes { get; set; } = [];
}