// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Helper extensions to compute relevance from heterogeneous source reference items.
/// </summary>
public static class SourceReferenceRelevanceExtensions
{
    /// <summary>
    /// Gets the highest relevance score available on this item.
    /// Supports both Kernel Memory and Vector Store reference items.
    /// </summary>
    public static double GetHighestRelevanceScore(this SourceReferenceItem item)
    {
        switch (item)
        {
            case KernelMemoryDocumentSourceReferenceItem km:
                return km.GetHighestScoringPartitionFromCitations();
            case VectorStoreAggregatedSourceReferenceItem vs:
                return vs.Chunks.Count == 0 ? vs.Score : Math.Max(vs.Score, vs.Chunks.Max(c => c.Relevance));
            default:
                return 0d;
        }
    }
}
