namespace Microsoft.Greenlight.Shared.Models.DocumentLibrary;

public class DocumentLibrary : EntityBase
{
    public required string ShortName { get; set; }
    public required string DescriptionOfContents { get; set; }
    public required string DescriptionOfWhenToUse { get; set; }
    public required string IndexName { get; set; }
    public required string BlobStorageContainerName { get; set; }
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    public List<DocumentLibraryDocumentProcessAssociation> DocumentProcessAssociations { get; set; } = new();
}