# Progressive Search Feature

## Overview

The Progressive Search feature has been implemented in `SemanticKernelVectorStoreRepository` to address relevance score challenges with large chunk sizes (particularly chunks over 1200 tokens). This feature provides automatic fallback strategies to find relevant content even when initial semantic similarity searches return no results.

## Problem Solved

Large chunks (e.g., 5000 tokens) naturally receive lower cosine similarity scores due to:
- **Semantic Dilution**: Large chunks contain diverse content, making embeddings less focused
- **Averaging Effect**: Embeddings represent the "average meaning" of the entire chunk
- **Query Mismatch**: Small, specific queries have lower similarity to large, general chunks

With default relevance thresholds (0.7), these large chunks often don't meet the minimum threshold, resulting in empty search results and failed content generation.

## How Progressive Search Works

The progressive search implements a **3-tier fallback strategy**:

### Tier 1: Broader Query Search
- Extracts meaningful terms from the original query (removes section numbers, short words)
- Increases search breadth (`Top` results increased to 15+)
- Uses original relevance threshold

### Tier 2: Progressive Relevance Reduction
- Tests progressively lower relevance thresholds: `[0.3, 0.2, 0.15, 0.1, 0.05]`
- Stops at the first threshold that returns results
- Designed specifically for large chunks that naturally score lower

### Tier 3: Keyword Fallback Search
- Extracts key terms from the query (filters out stop words)
- Searches individual terms with very low threshold (0.05)
- Provides last-resort content discovery

## Configuration Options

### ConsolidatedSearchOptions Properties

```csharp
public class ConsolidatedSearchOptions
{
    // ... existing properties ...

    /// <summary>
    /// Enable progressive search with multiple relevance thresholds. Default: true
    /// </summary>
    public bool EnableProgressiveSearch { get; set; } = true;

    /// <summary>
    /// Custom relevance thresholds for progressive search. Default: [0.3, 0.2, 0.15, 0.1, 0.05]
    /// </summary>
    public double[]? ProgressiveRelevanceThresholds { get; set; }

    /// <summary>
    /// Enable keyword-based fallback search as the final tier. Default: true
    /// </summary>
    public bool EnableKeywordFallback { get; set; } = true;
}
```

### Automatic Activation

Progressive search is automatically recommended when:
- Chunk size > 1200 tokens
- Using `SemanticKernelVectorStoreRepository`
- `EnableProgressiveSearch = true` (default)

## Usage Examples

### Basic Usage (Default Progressive Search)
```csharp
var searchOptions = new ConsolidatedSearchOptions
{
    DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
    IndexName = "my-index",
    Top = 10,
    MinRelevance = 0.7
    // EnableProgressiveSearch = true by default
};

var results = await repository.SearchAsync("library", "search query", searchOptions);
```

### Custom Progressive Thresholds
```csharp
var searchOptions = new ConsolidatedSearchOptions
{
    DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
    IndexName = "my-index",
    EnableProgressiveSearch = true,
    ProgressiveRelevanceThresholds = new[] { 0.4, 0.25, 0.1 } // Custom thresholds
};
```

### Disable Progressive Search
```csharp
var searchOptions = new ConsolidatedSearchOptions
{
    DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
    IndexName = "my-index",
    EnableProgressiveSearch = false // Strict relevance threshold
};
```

## Logging and Monitoring

The progressive search provides detailed logging to track which tier succeeded:

```
INFO: Initial search found 0 results, starting progressive search for index 'my-index'
INFO: Large chunk size detected (5000 tokens), progressive search recommended
DEBUG: Trying broader search with query 'facilities management' for index 'my-index'
INFO: Progressive search with threshold 0.15 found 3 results for index 'my-index'
```

## Integration with Existing Code

### RagBodyTextGenerator
The `RagBodyTextGenerator` automatically enables progressive search for `SemanticKernelVectorStore` processes:

```csharp
// Progressive search is now enabled automatically
if (IsSemanticKernelVectorStoreProcess())
{
    searchOptions.EnableProgressiveSearch = true;
}
```

### Kernel Memory Repositories
For Kernel Memory implementations, the progressive search flags are safely ignored, maintaining backward compatibility.

## Performance Considerations

- **Additional Search Calls**: Progressive search may perform 2-5 additional searches if initial search fails
- **Early Termination**: Stops at first successful tier, minimizing unnecessary calls
- **Caching**: Each tier uses the same embedding generation (cached by implementation)
- **Timeout**: Individual searches still respect standard timeout limits

## Best Practices

1. **Large Chunks**: Always enable progressive search for chunk sizes > 1200 tokens
2. **Strict Requirements**: Disable progressive search when exact relevance thresholds are critical
3. **Custom Thresholds**: Adjust thresholds based on your content and similarity score distributions
4. **Monitoring**: Watch logs to understand which tiers are being used most frequently

## Migration Guide

Existing code using `ConsolidatedSearchOptions` will continue to work unchanged:
- Progressive search is **enabled by default**
- No breaking changes to existing APIs
- Kernel Memory repositories safely ignore new properties

## Configuration Recommendations

Based on chunk size analysis:

| Chunk Size | Recommended Settings |
|------------|---------------------|
| < 1000 tokens | `EnableProgressiveSearch = false` (optional) |
| 1000-2000 tokens | `EnableProgressiveSearch = true` (default) |
| 2000-5000 tokens | `EnableProgressiveSearch = true` + custom thresholds `[0.25, 0.15, 0.08]` |
| > 5000 tokens | `EnableProgressiveSearch = true` + lower thresholds `[0.2, 0.1, 0.05]` |

This feature ensures robust content discovery regardless of chunk size while maintaining the flexibility to disable it when strict relevance requirements are needed.