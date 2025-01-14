using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents the information of a document process.
/// </summary>
public class DocumentProcessInfo : IDocumentProcessInfo
{
    /// <summary>
    /// Unique identifier of the document process.
    /// </summary>
    public Guid Id { get; set; } = Guid.Empty;

    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public string ShortName { get; set; }

    /// <summary>
    /// Description of the document process.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of repositories associated with the document process.
    /// </summary>
    public List<string> Repositories { get; set; } = [];

    /// <summary>
    /// Logic type used for the document process.
    /// </summary>
    public DocumentProcessLogicType LogicType { get; set; } = DocumentProcessLogicType.KernelMemory;

    /// <summary>
    /// Name of the blob storage container for the document process.
    /// </summary>
    public string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Type of Completion Service used for this Document Process
    /// </summary>
    public DocumentProcessCompletionServiceType? CompletionServiceType { get; set; } = DocumentProcessCompletionServiceType.GenericAiCompletionService;

    /// <summary>
    /// Name of the auto import folder in blob storage for the document process.
    /// </summary>
    public string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    /// <summary>
    /// Value indicating whether to classify documents.
    /// </summary>
    public bool ClassifyDocuments { get; set; } = false;

    /// <summary>
    /// Name of the classification model.
    /// </summary>
    public string? ClassificationModelName { get; set; }

    /// <summary>
    /// Gets the source of the document process.
    /// </summary>
    public ProcessSource Source => Id == Guid.Empty ? ProcessSource.Static : ProcessSource.Dynamic;

    /// <summary>
    /// Unique identifier for the document outline.
    /// </summary>
    public Guid? DocumentOutlineId { get; set; }

    /// <summary>
    /// Outline text of the document.
    /// </summary>
    public string? OutlineText { get; set; }

    /// <summary>
    /// Number of preceding search partitions to include.
    /// </summary>
    public int PrecedingSearchPartitionInclusionCount { get; set; } = 0;

    /// <summary>
    /// Number of following search partitions to include.
    /// </summary>
    public int FollowingSearchPartitionInclusionCount { get; set; } = 0;

    /// <summary>
    /// Number of citations to get from the repository.
    /// </summary>
    public int NumberOfCitationsToGetFromRepository { get; set; } = 50;

    /// <summary>
    /// Minimum relevance threshold for citations.
    /// </summary>
    public double MinimumRelevanceForCitations { get; set; } = 0.7;
}
