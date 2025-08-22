using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts
{
    /// <summary>
    /// Options for performing a consolidated search.
    /// </summary>
    public class ConsolidatedSearchOptions
    {
        /// <summary>
        /// Type of document library.
        /// </summary>
        public required DocumentLibraryType DocumentLibraryType { get; set; }

        /// <summary>
        /// Name of the index.
        /// </summary>
        public required string IndexName { get; set; } = "default";

        /// <summary>
        /// Parameters for exact match.
        /// </summary>
        public Dictionary<string, string> ParametersExactMatch { get; set; } = [];

        /// <summary>
        /// Number of top results to return.
        /// </summary>
        public int Top { get; set; } = 12;

        /// <summary>
        /// Minimum relevance score to return.
        /// </summary>
        public double MinRelevance { get; set; } = 0.7;

        /// <summary>
        /// Number of preceding partitions.
        /// </summary>
        public int PrecedingPartitionCount { get; set; } = 0;

        /// <summary>
        /// Number of following partitions.
        /// </summary>
        public int FollowingPartitionCount { get; set; } = 0;

        /// <summary>
        /// Enable progressive search with multiple relevance thresholds.
        /// When enabled, if initial search finds no results, the repository will try
        /// progressively lower relevance thresholds. Particularly useful for large chunks
        /// that naturally score lower in similarity searches. Default is true.
        /// </summary>
        public bool EnableProgressiveSearch { get; set; } = true;

        /// <summary>
        /// Custom relevance thresholds for progressive search.
        /// If null, uses default progressive thresholds. Only used when EnableProgressiveSearch is true.
        /// Thresholds should be in descending order (highest to lowest relevance).
        /// </summary>
        public double[]? ProgressiveRelevanceThresholds { get; set; }

        /// <summary>
        /// Enable keyword-based fallback search as the final tier.
        /// When enabled, if progressive relevance search fails, the repository will attempt
        /// keyword-based searches using extracted terms from the query. Default is true.
        /// </summary>
        public bool EnableKeywordFallback { get; set; } = true;
    }

}