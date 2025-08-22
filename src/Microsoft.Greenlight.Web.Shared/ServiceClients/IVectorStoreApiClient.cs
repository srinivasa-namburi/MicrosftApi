// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client for vector store search endpoints.
/// </summary>
public interface IVectorStoreApiClient : IServiceClient
{
    Task<ConsolidatedSearchOptions?> GetOptionsAsync(DocumentLibraryType type, string shortName);
    Task<List<VectorStoreSourceReferenceItemInfo>> SearchAsync(DocumentLibraryType type, string shortName, string query);
    Task<List<VectorStoreSourceReferenceItemInfo>> SearchAsync(DocumentLibraryType type, string shortName, string query, ConsolidatedSearchOptions overrideOptions);
}
