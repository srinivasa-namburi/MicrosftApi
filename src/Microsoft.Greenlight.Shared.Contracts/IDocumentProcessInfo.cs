using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts;

/// <summary>
/// Interface representing the information required for processing a document.
/// </summary>
public interface IDocumentProcessInfo
{
    /// <summary>
    /// Unique identifier of the document process.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// Short name of the document process.
    /// </summary>
    string ShortName { get; set; }

    /// <summary>
    /// Description of the document process.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// List of repositories associated with the document process.
    /// </summary>
    List<string> Repositories { get; set; }

    /// <summary>
    /// Logic type of the document process.
    /// </summary>
    DocumentProcessLogicType LogicType { get; set; }

    /// <summary>
    /// Name of the blob storage container.
    /// </summary>
    string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Name of the auto-import folder in the blob storage.
    /// </summary>
    string BlobStorageAutoImportFolderName { get; set; }

    /// <summary>
    /// Value indicating whether documents should be classified.
    /// </summary>
    bool ClassifyDocuments { get; set; }

    /// <summary>
    /// Name of the classification model.
    /// </summary>
    string? ClassificationModelName { get; set; }
}
