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
    }

}