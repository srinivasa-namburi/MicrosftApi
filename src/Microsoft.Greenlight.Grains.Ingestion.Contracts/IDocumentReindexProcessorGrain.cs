// Copyright (c) Microsoft Corporation. All rights reserved.

using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts;

/// <summary>
/// Grain contract for processing individual document reindexing operations.
/// </summary>
public interface IDocumentReindexProcessorGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Starts the reindexing process for a specific ingested document.
    /// </summary>
    /// <param name="ingestedDocumentId">The ID of the ingested document to reindex.</param>
    /// <param name="reason">The reason for reindexing.</param>
    /// <param name="orchestrationId">ID of the Orchestration grain starting this reindex process</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartReindexingAsync(Guid ingestedDocumentId, string reason, string orchestrationId);


    /// <summary>
    /// Gets whether this grain is currently active and processing.
    /// </summary>
    /// <returns>True if active, false otherwise.</returns>
    Task<bool> IsActiveAsync();
}