namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents an item in the list of generated documents.
/// </summary>
public class GeneratedDocumentListItem : DtoBase
{
    /// <summary>
    /// Title of the document.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Date when the document was generated.
    /// </summary>
    public DateTime GeneratedDate { get; set; }

    /// <summary>
    /// OID of the requesting author.
    /// </summary>
    public Guid RequestingAuthorOid { get; set; }
}
