namespace Microsoft.Greenlight.Shared.Configuration;

/// <summary>
/// Options for processing documents.
/// </summary>
public class DocumentProcessOptions
{
    /// <summary>
    /// Name of the document process.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Name of the blob storage container to store documents.
    /// </summary>
    public string? BlobStorageContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the auto import folder in blob storage to store documents.
    /// </summary>
    public string? BlobStorageAutoImportFolderName { get; set; } = string.Empty;

    /// <summary>
    /// Value indicating whether to classify documents.
    /// </summary>
    public bool ClassifyDocuments { get; set; }

    /// <summary>
    /// Name of the classification model.
    /// </summary>
    public string? ClassificationModelName { get; set; }

    /// <summary>
    /// Ingestion method which defaults to Classic.
    /// </summary>
    public string? IngestionMethod { get; set; } = "Classic";

    /// <summary>
    /// List of repositories.
    /// </summary>
    public List<string> Repositories { get; set; } = new List<string>();
}
