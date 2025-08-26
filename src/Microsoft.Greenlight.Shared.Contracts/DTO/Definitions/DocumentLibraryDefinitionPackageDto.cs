// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Portable export/import package for a Document Library definition.
    /// </summary>
    public class DocumentLibraryDefinitionPackageDto
    {
        /// <summary>
        /// Package format version.
        /// </summary>
        public string PackageFormatVersion { get; set; } = "1.0";

        /// <summary>
        /// Original library ID from source system.
        /// </summary>
        public Guid OriginalLibraryId { get; set; }

        /// <summary>
        /// Short name of the library (editable prior to import).
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// Description of contents.
        /// </summary>
        public string DescriptionOfContents { get; set; } = string.Empty;

        /// <summary>
        /// Description of when to use.
        /// </summary>
        public string DescriptionOfWhenToUse { get; set; } = string.Empty;

        /// <summary>
        /// Index name used by the vector store.
        /// </summary>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Container and auto-import settings.
        /// </summary>
        public string BlobStorageContainerName { get; set; } = string.Empty;
        public string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

        /// <summary>
        /// Logic type for the library.
        /// </summary>
        public DocumentProcessLogicType LogicType { get; set; } = DocumentProcessLogicType.SemanticKernelVectorStore;

        /// <summary>
        /// Vector store chunking settings.
        /// </summary>
        public int? VectorStoreChunkSize { get; set; }
        public int? VectorStoreChunkOverlap { get; set; }
    }
}
