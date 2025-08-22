// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

/// <summary>
/// WebAssembly implementation of vector store API client.
/// </summary>
public sealed class VectorStoreApiClient : WebAssemblyBaseServiceClient<VectorStoreApiClient>, IVectorStoreApiClient
{
    public VectorStoreApiClient(HttpClient httpClient, ILogger<VectorStoreApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<ConsolidatedSearchOptions?> GetOptionsAsync(DocumentLibraryType type, string shortName)
    {
        var response = await SendGetRequestMessage($"/api/vector-store/options?type={(int)type}&shortName={Uri.EscapeDataString(shortName)}");
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<ConsolidatedSearchOptions>();
    }

    public async Task<List<VectorStoreSourceReferenceItemInfo>> SearchAsync(DocumentLibraryType type, string shortName, string query)
    {
        var response = await SendPostRequestMessage($"/api/vector-store/search?type={(int)type}&shortName={Uri.EscapeDataString(shortName)}&q={Uri.EscapeDataString(query)}", null);
        response?.EnsureSuccessStatusCode();
        var result = await response!.Content.ReadFromJsonAsync<List<VectorStoreSourceReferenceItemInfo>>();
        return result ?? new List<VectorStoreSourceReferenceItemInfo>();
    }

    public async Task<List<VectorStoreSourceReferenceItemInfo>> SearchAsync(DocumentLibraryType type, string shortName, string query, ConsolidatedSearchOptions overrideOptions)
    {
        var response = await SendPostRequestMessage($"/api/vector-store/search?type={(int)type}&shortName={Uri.EscapeDataString(shortName)}&q={Uri.EscapeDataString(query)}", overrideOptions);
        response?.EnsureSuccessStatusCode();
        var result = await response!.Content.ReadFromJsonAsync<List<VectorStoreSourceReferenceItemInfo>>();
        return result ?? new List<VectorStoreSourceReferenceItemInfo>();
    }
}
