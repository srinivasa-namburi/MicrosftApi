// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// v2 package descriptor for creating/associating a FileStorageSource
    /// with a Document Process or Document Library during import.
    ///
    /// The host is implicitly the system's default FileStorageHost; on import we
    /// will bind to the single default host present in the installation.
    /// </summary>
    public class FileStorageSourceConfigDto
    {
        /// <summary>
        /// Container (for blob) or path (for local/other providers).
        /// </summary>
        public string ContainerOrPath { get; set; } = string.Empty;

        /// <summary>
        /// Optional auto-import folder within the container/path.
        /// </summary>
        public string? AutoImportFolderName { get; set; } = "ingest-auto";

        /// <summary>
        /// Whether this source should accept user uploads for this association.
        /// Only one association per process/library should be marked true.
        /// </summary>
        public bool AcceptsUploads { get; set; } = true;

        /// <summary>
        /// Priority order for processing files from this source (lower numbers first).
        /// </summary>
        public int Priority { get; set; } = 1;
    }
}

