using System.ComponentModel.DataAnnotations;
using Microsoft.Greenlight.Shared.Enums;

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
    /// Logic type used for the document library.
    /// </summary>
    public DocumentProcessLogicType LogicType { get; set; } = DocumentProcessLogicType.SemanticKernelVectorStore;

    /// <summary>
    /// Document process associations of the document library.
    /// </summary>
    public List<DocumentLibraryDocumentProcessAssociationInfo> DocumentProcessAssociations { get; set; } = [];

    /// <summary>
    /// Vector store chunking mode (only applies when LogicType is SemanticKernelVectorStore). Defaults to Simple.
    /// </summary>
    public TextChunkingMode? VectorStoreChunkingMode { get; set; } = TextChunkingMode.Simple;

    /// <summary>
    /// Vector store chunk size in tokens (only applies when LogicType is SemanticKernelVectorStore). If null, global VectorStoreOptions.ChunkSize is used.
    /// </summary>
    public int? VectorStoreChunkSize { get; set; }

    /// <summary>
    /// Vector store chunk overlap in tokens (only applies when LogicType is SemanticKernelVectorStore). If null, global VectorStoreOptions.ChunkOverlap is used.
    /// </summary>
    public int? VectorStoreChunkOverlap { get; set; }
}