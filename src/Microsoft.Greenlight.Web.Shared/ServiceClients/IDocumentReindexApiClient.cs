// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client interface for document reindexing operations.
/// </summary>
public interface IDocumentReindexApiClient : IServiceClient
{
    /// <summary>
    /// Starts reindexing for a document library.
    /// </summary>
    /// <param name="documentLibraryShortName">The short name of the document library.</param>
    /// <param name="reason">The reason for reindexing.</param>
    /// <returns>The orchestration ID for the reindexing operation.</returns>
    Task<string> StartDocumentLibraryReindexingAsync(string documentLibraryShortName, string reason);

    /// <summary>
    /// Starts reindexing for a document process.
    /// </summary>
    /// <param name="documentProcessShortName">The short name of the document process.</param>
    /// <param name="reason">The reason for reindexing.</param>
    /// <returns>The orchestration ID for the reindexing operation.</returns>
    Task<string> StartDocumentProcessReindexingAsync(string documentProcessShortName, string reason);

    /// <summary>
    /// Gets the status of a reindexing operation.
    /// </summary>
    /// <param name="orchestrationId">The orchestration ID of the reindexing operation.</param>
    /// <returns>The current status of the reindexing operation.</returns>
    Task<DocumentReindexStateInfo?> GetReindexingStatusAsync(string orchestrationId);
}