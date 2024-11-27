using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

public class DocumentLibraryInfo
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Short Name is required.")]
    public string ShortName { get; set; }

    [Required(ErrorMessage = "Description of Contents is required.")]
    public string DescriptionOfContents { get; set; }

    [Required(ErrorMessage = "Description of When to Use is required.")]
    public string DescriptionOfWhenToUse { get; set; }

    [Required(ErrorMessage = "Index Name is required.")]
    public string IndexName { get; set; }

    [Required(ErrorMessage = "Blob Storage Container Name is required.")]
    public string BlobStorageContainerName { get; set; }

    [Required(ErrorMessage = "Blob Storage Auto Import Folder Name is required.")]
    public string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    public List<DocumentLibraryDocumentProcessAssociationInfo> DocumentProcessAssociations { get; set; } = [];
}