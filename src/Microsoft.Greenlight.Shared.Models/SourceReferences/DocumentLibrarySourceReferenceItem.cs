using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

public class DocumentLibrarySourceReferenceItem : KernelMemoryDocumentSourceReferenceItem
{
    public string? DocumentLibraryShortName { get; set; }

    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.AdditionalDocumentLibrary;
        Description = "Document fragments from additional document library";
        base.SetBasicParameters();
    }
}