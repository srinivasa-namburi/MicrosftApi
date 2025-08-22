// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IContentNodeApiClient : IServiceClient
{
    Task<ContentNodeInfo?> GetContentNodeAsync(string contentNodeId);
    Task<ContentNodeSystemItemInfo?> GetContentNodeSystemItemAsync(Guid contentNodeSystemItemId);
    Task<List<ContentNodeVersion>> GetContentNodeVersionsAsync(Guid contentNodeId);
    Task<ContentNodeInfo?> UpdateContentNodeTextAsync(Guid contentNodeId, string newText, ContentNodeVersioningReason reason, string? comment = null);
    Task<ContentNodeInfo?> PromoteContentNodeVersionAsync(Guid contentNodeId, Guid versionId, string? comment = null);
    Task<bool> HasPreviousVersionsAsync(Guid contentNodeId);

    /// <summary>
    /// Fetch specific vector-store chunks by index, documentId and partition numbers.
    /// </summary>
    Task<List<VectorStoreDocumentChunkInfo>> GetVectorChunksAsync(string index, string documentId, string partitionsCsv);
}
