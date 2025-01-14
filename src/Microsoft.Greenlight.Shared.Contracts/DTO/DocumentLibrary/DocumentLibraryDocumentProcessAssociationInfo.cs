namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

/// <summary>
/// Represents the association information between a document process and a document library.
/// </summary>
public class DocumentLibraryDocumentProcessAssociationInfo
{
    /// <summary>
    /// Unique identifier for the association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier for the document library.
    /// </summary>
    public Guid DocumentLibraryId { get; set; }

    /// <summary>
    /// Unique identifier for the dynamic document process definition.
    /// </summary>
    public Guid DynamicDocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public required string DocumentProcessShortName { get; set; }
}
