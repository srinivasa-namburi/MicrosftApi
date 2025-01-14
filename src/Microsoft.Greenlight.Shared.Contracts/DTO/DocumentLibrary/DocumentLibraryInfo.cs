using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

/// <summary>
/// Represents information about a document library.
/// </summary>
public class DocumentLibraryInfo
{
    /// <summary>
    /// Unique identifier of the document library.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short name of the document library.
    /// </summary>
    [Required(ErrorMessage = "Short Name is required.")]
    public string ShortName { get; set; }

    /// <summary>
    /// Description of the contents of the document library.
    /// </summary>
    [Required(ErrorMessage = "Description of Contents is required.")]
    public string DescriptionOfContents { get; set; }

    /// <summary>
    /// Description of when to use the document library.
    /// </summary>
    [Required(ErrorMessage = "Description of When to Use is required.")]
    public string DescriptionOfWhenToUse { get; set; }

    /// <summary>
    /// Index name for the document library.
    /// </summary>
    [Required(ErrorMessage = "Index Name is required.")]
    public string IndexName { get; set; }

    /// <summary>
    /// Blob storage container name for the document library.
    /// </summary>
    [Required(ErrorMessage = "Blob Storage Container Name is required.")]
    public string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Blob storage auto import folder name for the document library.
    /// </summary>
    [Required(ErrorMessage = "Blob Storage Auto Import Folder Name is required.")]
    public string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    /// <summary>
    /// Document process associations of the document library.
    /// </summary>
    public List<DocumentLibraryDocumentProcessAssociationInfo> DocumentProcessAssociations { get; set; } = [];
}