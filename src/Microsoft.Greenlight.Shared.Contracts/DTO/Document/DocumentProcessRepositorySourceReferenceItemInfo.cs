namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents information about a document process repository source reference item.
/// </summary>
public class DocumentProcessRepositorySourceReferenceItemInfo : KernelMemoryDocumentSourceReferenceItemInfo
{
    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public string? DocumentProcessShortName { get; set; }
}