// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable export/import package for a Document Process definition.
    /// This DTO is intentionally decoupled from backend EF models.
    /// </summary>
    public class DocumentProcessDefinitionPackageDto
    {
        /// <summary>
        /// Version string for forward compatibility.
        /// </summary>
        public string PackageFormatVersion { get; set; } = "1.0";

        /// <summary>
        /// Original process ID from the source system. Not used as primary key on import.
        /// </summary>
        public Guid OriginalProcessId { get; set; }

        /// <summary>
        /// Short name of the document process.
        /// May be edited before import to avoid collisions.
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// Description of the document process.
        /// May be edited before import.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Retrieval settings - preceding/following partition counts, citations, minimum relevance.
        /// </summary>
        public int PrecedingSearchPartitionInclusionCount { get; set; }
        public int FollowingSearchPartitionInclusionCount { get; set; }
        public int NumberOfCitationsToGetFromRepository { get; set; }
        public double MinimumRelevanceForCitations { get; set; }

        /// <summary>
        /// Vector store chunk settings for ingestion.
        /// May be edited before import.
        /// </summary>
        public int? VectorStoreChunkSize { get; set; }
        public int? VectorStoreChunkOverlap { get; set; }

        /// <summary>
        /// Optional storage settings. If omitted during import, server will generate defaults.
        /// </summary>
        public string? BlobStorageContainerName { get; set; }
        public string? BlobStorageAutoImportFolderName { get; set; }

        /// <summary>
        /// Prompt implementations scoped to the document process.
        /// Optional.
        /// </summary>
        public List<PromptImplementationPackageDto>? Prompts { get; set; }

        /// <summary>
        /// Optional outline to include with the process definition.
        /// </summary>
        public DocumentOutlinePackageDto? Outline { get; set; }

        /// <summary>
        /// Metadata fields associated with the process. Optional.
        /// </summary>
        public List<DocumentProcessMetaDataFieldPackageDto>? MetaDataFields { get; set; }
    }
}
