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
    public virtual string ShortName { get; set; }

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
    public DocumentProcessLogicType LogicType { get; set; } = DocumentProcessLogicType.SemanticKernelVectorStore;

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
    public int NumberOfCitationsToGetFromRepository { get; set; } = 10;

    /// <summary>
    /// Minimum relevance threshold for citations.
    /// </summary>
    public double MinimumRelevanceForCitations { get; set; } = 0.7;

    /// <summary>
    /// Vector store chunk size in tokens (only applies when LogicType is SemanticKernelVectorStore).
    /// If null, uses global VectorStoreOptions.ChunkSize.
    /// </summary>
    public int? VectorStoreChunkSize { get; set; }

    /// <summary>
    /// Vector store chunk overlap in tokens (only applies when LogicType is SemanticKernelVectorStore).
    /// If null, uses global VectorStoreOptions.ChunkOverlap.
    /// </summary>
    public int? VectorStoreChunkOverlap { get; set; }

    /// <summary>
    /// ID of the validation pipeline associated with the document process. May be null.
    /// </summary>
    public Guid? ValidationPipelineId { get; set; }

    /// <summary>
    /// ID of the Flow Task template associated with the document process for conversational document generation. May be null.
    /// </summary>
    public Guid? FlowTaskTemplateId { get; set; }

    /// <summary>
    /// Unique identifier of the AI model deployment.
    /// </summary>
    public Guid? AiModelDeploymentId { get; set; }

    /// <summary>
    /// Unique identifier of the AI model deployment used for validation.
    /// </summary>
    public Guid? AiModelDeploymentForValidationId { get; set; }

    /// <summary>
    /// Unique identifier of the embedding model deployment used for this document process.
    /// If null, uses the global default embedding model.
    /// </summary>
    public Guid? EmbeddingModelDeploymentId { get; set; }

    /// <summary>
    /// Vector store chunking mode (only applies when LogicType is SemanticKernelVectorStore). Defaults to Simple.
    /// </summary>
    public TextChunkingMode? VectorStoreChunkingMode { get; set; } = TextChunkingMode.Simple;

    /// <summary>
    /// Optional override for embedding vector dimensions; if null, the deployment's embedding size is used.
    /// </summary>
    public int? EmbeddingDimensionsOverride { get; set; }
}
