// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Grains.Ingestion.Contracts.State;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts;

/// <summary>
/// Grain contract for orchestrating document reindexing operations.
/// </summary>
public interface IDocumentReindexOrchestrationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Starts the reindexing process for a document library.
    /// </summary>
    /// <param name="documentLibraryShortName">The short name of the document library.</param>
    /// <param name="documentLibraryType">The type of document library.</param>
    /// <param name="reason">The reason for reindexing (e.g., "Chunk size changed").</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartReindexingAsync(
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType,
        string reason);

    /// <summary>
    /// Starts the reindexing process for a document process.
    /// </summary>
    /// <param name="documentProcessShortName">The short name of the document process.</param>
    /// <param name="reason">The reason for reindexing (e.g., "Chunk size changed").</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDocumentProcessReindexingAsync(
        string documentProcessShortName,
        string reason);

    /// <summary>
    /// Starts the reindexing process for a document library.
    /// </summary>
    /// <param name="documentLibraryShortName">The short name of the document library.</param>
    /// <param name="reason">The reason for reindexing (e.g., "Chunk size changed").</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDocumentLibraryReindexingAsync(
        string documentLibraryShortName,
        string reason);

    /// <summary>
    /// Gets the current state of the reindexing operation.
    /// </summary>
    /// <returns>The current reindexing state.</returns>
    Task<DocumentReindexState> GetStateAsync();

    /// <summary>
    /// Returns true if this orchestration activation is currently running.
    /// This reflects in-memory activity and is used to avoid stale persisted states after restarts.
    /// </summary>
    Task<bool> IsRunningAsync();

    /// <summary>
    /// Called when a document reindexing operation completes successfully.
    /// </summary>
    /// <param name="documentId">The ID of the document that was reindexed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnReindexCompletedAsync(Guid documentId);

    /// <summary>
    /// Called when a document reindexing operation fails.
    /// </summary>
    /// <param name="documentId">The ID of the document that failed to reindex.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="acquired">Whether a semaphore was acquired for this operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnReindexFailedAsync(Guid documentId, string reason, bool acquired);

    /// <summary>
    /// Legacy method: Called when a document reindexing operation completes successfully (without document ID).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnReindexCompletedAsync();

    /// <summary>
    /// Legacy method: Called when a document reindexing operation fails (without document ID).
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="acquired">Whether a semaphore was acquired for this operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnReindexFailedAsync(string reason, bool acquired);

    /// <summary>
    /// Requests this orchestration grain to deactivate as soon as possible.
    /// Used during cleanup (e.g., library deletion) to avoid stale activations.
    /// </summary>
    Task DeactivateAsync();
}