namespace Microsoft.Greenlight.Shared.Models.DocumentLibrary;

/// <summary>
/// Represents a document library with various properties and associations.
/// </summary>
public class DocumentLibrary : EntityBase
{
    /// <summary>
    /// Short name of the document library.
    /// </summary>
    public required string ShortName { get; set; }

    /// <summary>
    /// Description of the contents of the document library.
    /// </summary>
    public required string DescriptionOfContents { get; set; }

    /// <summary>
    /// Description of when to use the document library.
    /// </summary>
    public required string DescriptionOfWhenToUse { get; set; }

    /// <summary>
    /// Index name of the document library.
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Name of the blob storage container where documents are stored.
    /// </summary>
    public required string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Name of the auto import folder in the blob storage.
    /// </summary>
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    /// <summary>
    /// List of document process associations.
    /// </summary>
    public List<DocumentLibraryDocumentProcessAssociation> DocumentProcessAssociations { get; set; } = [];
}
