// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.Definitions;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

/// <summary>
/// WebAssembly client for definitions import/export endpoints.
/// </summary>
public class DefinitionsApiClient : WebAssemblyBaseServiceClient<DefinitionsApiClient>, IDefinitionsApiClient
{
    public DefinitionsApiClient(HttpClient httpClient, ILogger<DefinitionsApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<DocumentProcessDefinitionPackageDto?> ExportProcessAsync(Guid processId)
    {
        var resp = await SendGetRequestMessage($"/api/definitions/process/{processId}/export");
        if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<DocumentProcessDefinitionPackageDto>();
    }

    public async Task<Guid> ImportProcessAsync(DocumentProcessDefinitionPackageDto package)
    {
        var resp = await SendPostRequestMessage("/api/definitions/process/import", package);
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<bool> IsProcessShortNameAvailableAsync(string shortName)
    {
        var resp = await SendGetRequestMessage($"/api/definitions/process/check-shortname/{Uri.EscapeDataString(shortName)}");
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<bool>();
    }

    public async Task<DocumentLibraryDefinitionPackageDto?> ExportLibraryAsync(Guid libraryId)
    {
        var resp = await SendGetRequestMessage($"/api/definitions/library/{libraryId}/export");
        if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<DocumentLibraryDefinitionPackageDto>();
    }

    public async Task<Guid> ImportLibraryAsync(DocumentLibraryDefinitionPackageDto package)
    {
        var resp = await SendPostRequestMessage("/api/definitions/library/import", package);
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<bool> IsLibraryShortNameAvailableAsync(string shortName)
    {
        var resp = await SendGetRequestMessage($"/api/definitions/library/check-shortname/{Uri.EscapeDataString(shortName)}");
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<bool>();
    }

    public async Task<IndexCompatibilityInfo?> GetIndexCompatibilityAsync(string indexName)
    {
        var resp = await SendGetRequestMessage($"/api/definitions/index/compatibility/{Uri.EscapeDataString(indexName)}");
        if (resp?.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp?.EnsureSuccessStatusCode();
        return await resp!.Content.ReadFromJsonAsync<IndexCompatibilityInfo>();
    }
}
