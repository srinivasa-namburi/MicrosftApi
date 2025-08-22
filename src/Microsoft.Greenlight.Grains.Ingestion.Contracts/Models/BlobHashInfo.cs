// Copyright (c) Microsoft Corporation. All rights reserved.
using System;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts.Models
{
    /// <summary>
    /// Hash information for a single blob under a container/prefix.
    /// </summary>
    public class BlobHashInfo
    {
        /// <summary>
        /// Full blob path (within container) as listed by enumeration.
        /// </summary>
        public required string BlobName { get; set; }
        /// <summary>
        /// Relative file name (leaf) derived from BlobName.
        /// </summary>
        public required string RelativeFileName { get; set; }
        /// <summary>
        /// Full URL to the blob.
        /// </summary>
        public required string FullBlobUrl { get; set; }
        /// <summary>
        /// Computed hash (null if hashing failed).
        /// </summary>
        public string? Hash { get; set; }
    }
}
