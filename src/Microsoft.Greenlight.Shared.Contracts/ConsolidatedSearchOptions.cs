using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts
{
    public class ConsolidatedSearchOptions
    {
        public required DocumentLibraryType DocumentLibraryType { get; set; }
        public required string IndexName { get; set; } = "default";
        public Dictionary<string, string> ParametersExactMatch { get; set; } = new();
        public int Top { get; set; } = 12;
        public double MinRelevance { get; set; } = 0.7;
        public int PrecedingPartitionCount { get; set; } = 0;
        public int FollowingPartitionCount { get; set; } = 0;
    }

}