using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Represents a source reference item for document process repository.
/// </summary>
public class DocumentProcessRepositorySourceReferenceItem : KernelMemoryDocumentSourceReferenceItem
{
    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public string? DocumentProcessShortName { get; set; }

    /// <summary>
    /// Basic parameters for the source reference item.
    /// </summary>
    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.DocumentProcessRepository;
        Description = "Document fragments from primary document process knowledge repository";
        base.SetBasicParameters();
    }
}
