// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Models.FileStorage;

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
    /// Legacy property - use FileStorageSources for new implementations.
    /// </summary>
    public required string BlobStorageContainerName { get; set; }

    /// <summary>
    /// Name of the auto import folder in the blob storage.
    /// Legacy property - use FileStorageSources for new implementations.
    /// </summary>
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    /// <summary>
    /// Processing logic type used by this document library.
    /// Existing libraries (pre-Aug 2025) default to KernelMemory via migration default; new libraries default to SemanticKernelVectorStore via DTO default.
    /// </summary>
    public Microsoft.Greenlight.Shared.Enums.DocumentProcessLogicType LogicType { get; set; } = Microsoft.Greenlight.Shared.Enums.DocumentProcessLogicType.KernelMemory;

    /// <summary>
    /// List of document process associations.
    /// </summary>
    public List<DocumentLibraryDocumentProcessAssociation> DocumentProcessAssociations { get; set; } = [];

    /// <summary>
    /// File storage sources associated with this document library.
    /// </summary>
    [JsonIgnore]
    public List<DocumentLibraryFileStorageSource> FileStorageSources { get; set; } = [];

    /// <summary>
    /// Unique identifier of the embedding model deployment used for this document library.
    /// If null, uses the global default embedding model.
    /// </summary>
    public Guid? EmbeddingModelDeploymentId { get; set; }

    /// <summary>
    /// Embedding model deployment associated with the document library
    /// </summary>
    [JsonIgnore]
    public AiModelDeployment? EmbeddingModelDeployment { get; set; }

    /// <summary>
    /// Chunking mode for vector store ingestion (only applies when LogicType is SemanticKernelVectorStore).
    /// Defaults to Simple.
    /// </summary>
    public Microsoft.Greenlight.Shared.Enums.TextChunkingMode? VectorStoreChunkingMode { get; set; } = Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Simple;

    /// <summary>
    /// Vector store chunk size in tokens (only applies when LogicType is SemanticKernelVectorStore). If null, global VectorStoreOptions.ChunkSize is used.
    /// </summary>
    public int? VectorStoreChunkSize { get; set; }

    /// <summary>
    /// Vector store chunk overlap in tokens (only applies when LogicType is SemanticKernelVectorStore). If null, global VectorStoreOptions.ChunkOverlap is used.
    /// </summary>
    public int? VectorStoreChunkOverlap { get; set; }

    /// <summary>
    /// Optional override for embedding vector dimensions; if null, the deployment's embedding size is used.
    /// </summary>
    public int? EmbeddingDimensionsOverride { get; set; }
}
