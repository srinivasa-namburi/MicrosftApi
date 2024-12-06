using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

public class DocumentProcessRepositorySourceReferenceItem : KernelMemoryDocumentSourceReferenceItem
{
    public string? DocumentProcessShortName { get; set; }

    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.DocumentProcessRepository;
        Description = "Document fragments from primary document process knowledge repository";
        base.SetBasicParameters();
    }
}