// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

/// <summary>
/// HTTP API client for document reindexing operations.
/// </summary>
public class DocumentReindexApiClient : WebAssemblyBaseServiceClient<DocumentReindexApiClient>, IDocumentReindexApiClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentReindexApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="authStateProvider">The authentication state provider.</param>
    public DocumentReindexApiClient(HttpClient httpClient, ILogger<DocumentReindexApiClient> logger, AuthenticationStateProvider authStateProvider) 
        : base(httpClient, logger, authStateProvider)
    {
    }

    /// <inheritdoc />
    public async Task<string> StartDocumentLibraryReindexingAsync(string documentLibraryShortName, string reason)
    {
        var response = await SendPostRequestMessage($"/api/reindex/document-library/{documentLibraryShortName}", reason);
        response?.EnsureSuccessStatusCode();
        
        var result = await response?.Content.ReadFromJsonAsync<ReindexStartResponse>();
        return result?.OrchestrationId ?? throw new InvalidOperationException("Failed to get orchestration ID from response");
    }

    /// <inheritdoc />
    public async Task<string> StartDocumentProcessReindexingAsync(string documentProcessShortName, string reason)
    {
        var response = await SendPostRequestMessage($"/api/reindex/document-process/{documentProcessShortName}", reason);
        response?.EnsureSuccessStatusCode();
        
        var result = await response?.Content.ReadFromJsonAsync<ReindexStartResponse>();
        return result?.OrchestrationId ?? throw new InvalidOperationException("Failed to get orchestration ID from response");
    }

    /// <inheritdoc />
    public async Task<DocumentReindexStateInfo?> GetReindexingStatusAsync(string orchestrationId)
    {
        var response = await SendGetRequestMessage($"/api/reindex/status/{orchestrationId}");
        
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<DocumentReindexStateInfo>();
    }
}