// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document
{
    /// <summary>
    /// DTO representing an ingested document for contract boundaries.
    /// </summary>
    public class IngestedDocumentInfo
    {
        /// <summary>
        /// Unique identifier for the ingested document.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// File name of the ingested document.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File hash of the ingested document.
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// The original blob URL of the file before it is copied.
        /// </summary>
        public string OriginalDocumentUrl { get; set; } = string.Empty;

        /// <summary>
        /// The final blob URL of the file after it has been copied to its destination.
        /// </summary>
        public string FinalBlobUrl { get; set; } = string.Empty;

        /// <summary>
        /// OID of the user who uploaded the document.
        /// </summary>
        public string? UploadedByUserOid { get; set; }

        /// <summary>
        /// Name of the document library or process associated with the document.
        /// </summary>
        public string? DocumentLibraryOrProcessName { get; set; }

        /// <summary>
        /// The type of document ingestion (library or process).
        /// </summary>
        public DocumentLibraryType DocumentLibraryType { get; set; }

        /// <summary>
        /// Ingestion state of the document.
        /// </summary>
        public IngestionState IngestionState { get; set; }

        /// <summary>
        /// Date when the document was ingested.
        /// </summary>
        public DateTime IngestedDate { get; set; }

        /// <summary>
        /// The container in which the file resides.
        /// </summary>
        public string Container { get; set; } = string.Empty;

        /// <summary>
        /// The folder path within the container.
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// The orchestration ID (SHA256 of container + folder) for this ingestion batch.
        /// </summary>
        public string OrchestrationId { get; set; } = string.Empty;

        /// <summary>
        /// Error message if ingestion failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// UTC date and time when the entity was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// UTC date and time when the entity was last modified.
        /// </summary>
        public DateTime ModifiedUtc { get; set; }
    }
}
