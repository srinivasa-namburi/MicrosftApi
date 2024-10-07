namespace Microsoft.Greenlight.Shared.Configuration;

public class DocumentProcessOptions
{
    public string Name { get; set; } = string.Empty;
    public string? BlobStorageContainerName { get; set; } = string.Empty;
    public string? BlobStorageAutoImportFolderName { get; set; } = string.Empty;
    public bool ClassifyDocuments { get; set; }
    public string? ClassificationModelName { get; set; }
    public string? IngestionMethod { get; set; } = "Classic";
    public List<string> Repositories { get; set; } = new List<string>();

}
