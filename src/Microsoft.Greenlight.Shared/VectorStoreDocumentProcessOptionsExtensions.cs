// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared;

/// <summary>
/// Extension factory methods for building VectorStoreDocumentProcessOptions from backend models.
/// Placed in Shared assembly to avoid taking model references in the Configuration project.
/// </summary>
public static class VectorStoreDocumentProcessOptionsExtensions
{
    /// <summary>
    /// Creates options from a backend DynamicDocumentProcessDefinition, falling back to global options where needed.
    /// </summary>
    public static VectorStoreDocumentProcessOptions FromDocumentProcess(
        this VectorStoreOptions globalOptions,
        DynamicDocumentProcessDefinition? documentProcess)
    {
        return new VectorStoreDocumentProcessOptions
        {
            ChunkSize = documentProcess?.VectorStoreChunkSize ?? globalOptions.ChunkSize,
            ChunkOverlap = documentProcess?.VectorStoreChunkOverlap ?? globalOptions.ChunkOverlap,
            MinRelevanceScore = documentProcess?.MinimumRelevanceForCitations ?? globalOptions.MinRelevanceScore,
            MaxSearchResults = documentProcess?.NumberOfCitationsToGetFromRepository ?? globalOptions.MaxSearchResults,
            ChunkingMode = documentProcess?.VectorStoreChunkingMode
        };
    }

    /// <summary>
    /// Creates options from a DocumentLibrary model. Now supports library-specific chunk size and overlap values.
    /// </summary>
    public static VectorStoreDocumentProcessOptions FromDocumentLibrary(
        this VectorStoreOptions globalOptions,
        DocumentLibrary? documentLibrary)
    {
        return new VectorStoreDocumentProcessOptions
        {
            ChunkSize = documentLibrary?.VectorStoreChunkSize ?? globalOptions.ChunkSize,
            ChunkOverlap = documentLibrary?.VectorStoreChunkOverlap ?? globalOptions.ChunkOverlap,
            MinRelevanceScore = globalOptions.MinRelevanceScore,
            MaxSearchResults = globalOptions.MaxSearchResults,
            ChunkingMode = documentLibrary?.VectorStoreChunkingMode
        };
    }
}
