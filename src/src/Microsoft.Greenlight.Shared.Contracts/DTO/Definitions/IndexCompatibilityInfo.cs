// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Definitions
{
    /// <summary>
    /// Describes compatibility information for an existing vector index/collection when using the SK Vector Store layout.
    /// </summary>
    public sealed class IndexCompatibilityInfo
    {
        /// <summary>
        /// Gets or sets the index/collection name that was evaluated.
        /// </summary>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// True if a backing index/collection appears to exist in the configured vector store provider.
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// True if the index appears to be compatible with the SK unified record layout (field names match).
        /// </summary>
        public bool IsSkLayout { get; set; }

        /// <summary>
        /// When the layout matches, the embedding vector dimensions that appear to work for this index.
        /// </summary>
        public int? MatchedEmbeddingDimensions { get; set; }

        /// <summary>
        /// Optional warnings detected during the compatibility check.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Optional error message if the check failed unexpectedly.
        /// </summary>
        public string? Error { get; set; }
    }
}
