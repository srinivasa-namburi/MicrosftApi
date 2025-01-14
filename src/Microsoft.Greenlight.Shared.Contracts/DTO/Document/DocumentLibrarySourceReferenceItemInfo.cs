namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a document library source reference item.
/// </summary>
public class DocumentLibrarySourceReferenceItemInfo : KernelMemoryDocumentSourceReferenceItemInfo
{
    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public string? DocumentLibraryShortName { get; set; }
}