using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

/// <summary>
/// Represents a dynamic document process definition with various properties and associations.
/// </summary>
public class DynamicDocumentProcessDefinition : EntityBase, IDocumentProcessInfo
{
    /// <summary>
    /// Short name of the document process.
    /// </summary>
    public required string ShortName { get; set; }

    /// <summary>
    /// Description of the document process.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of repositories associated with the document process.
    /// </summary>
    public List<string> Repositories { get; set; } = [];

    /// <summary>
    /// Logic type of the document process.
    /// </summary>
    public DocumentProcessLogicType LogicType { get; set; }

    /// <summary>
    /// Status of the document process.
    /// </summary>
    public DocumentProcessStatus Status { get; set; } = DocumentProcessStatus.Created;

    /// <summary>
    /// Type of completion service for the document process.
    /// </summary>
    public DocumentProcessCompletionServiceType CompletionServiceType { get; set; } =
        DocumentProcessCompletionServiceType.GenericAiCompletionService;

    /// <summary>
    /// Name of the blob storage container where documents are stored.
    /// </summary>
    public required string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Name of the auto-import folder in the blob storage.
    /// </summary>
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    /// <summary>
    /// Indicates whether documents should be classified.
    /// </summary>
    public bool ClassifyDocuments { get; set; }

    /// <summary>
    /// Name of the classification model.
    /// </summary>
    public string? ClassificationModelName { get; set; }

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
    public int NumberOfCitationsToGetFromRepository { get; set; } = 12;

    /// <summary>
    /// Minimum relevance score for citations.
    /// </summary>
    public double MinimumRelevanceForCitations { get; set; } = 0.7;

    /// <summary>
    /// Unique identifier of the document outline.
    /// </summary>
    public Guid? DocumentOutlineId { get; set; }

    /// <summary>
    /// Document outline associated with the document process.
    /// </summary>
    public DocumentOutline? DocumentOutline { get; set; }

    /// <summary>
    /// List of prompt implementations associated with the document process.
    /// </summary>
    public List<PromptImplementation> Prompts { get; set; } = [];

    /// <summary>
    /// List of plugins associated with the document process.
    /// </summary>
    public List<DynamicPluginDocumentProcess>? Plugins { get; set; } = [];

    /// <summary>
    /// List of additional document libraries associated with the document process.
    /// </summary>
    public List<DocumentLibraryDocumentProcessAssociation>? AdditionalDocumentLibraries { get; set; } = [];

    /// <summary>
    /// List of metadata fields associated with the document process.
    /// </summary>
    public List<DynamicDocumentProcessMetaDataField> MetaDataFields { get; set; } = [];
}
