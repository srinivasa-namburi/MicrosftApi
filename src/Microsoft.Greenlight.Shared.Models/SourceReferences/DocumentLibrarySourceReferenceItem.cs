using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a source reference item for a document library.
/// </summary>
public class DocumentLibrarySourceReferenceItem : KernelMemoryDocumentSourceReferenceItem
{
    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public string? DocumentLibraryShortName { get; set; }

    /// <summary>
    /// Sets the basic parameters for the document library source reference item.
    /// </summary>
    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.AdditionalDocumentLibrary;
        Description = "Document fragments from additional document library";
        base.SetBasicParameters();
    }
}
