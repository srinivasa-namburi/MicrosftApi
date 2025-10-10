// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class McpConfigurationApiClient : WebAssemblyBaseServiceClient<McpConfigurationApiClient>, IMcpConfigurationApiClient
{
    public McpConfigurationApiClient(HttpClient httpClient, ILogger<McpConfigurationApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<McpConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendGetRequestMessage("/api/mcp-config");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<McpConfigurationModel>(cancellationToken: cancellationToken) ?? new McpConfigurationModel();
    }

    public async Task UpdateAsync(McpConfigurationModel model, CancellationToken cancellationToken = default)
    {
        var response = await SendPutRequestMessage("/api/mcp-config", model);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.McpSecretInfo>> ListSecretsAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendGetRequestMessage("/api/mcp-config/secrets");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.McpSecretInfo>>(cancellationToken: cancellationToken) 
               ?? new List<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.McpSecretInfo>();
    }

    public async Task<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretResponse> CreateSecretAsync(Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendPostRequestMessage("/api/mcp-config/secrets", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretResponse>(cancellationToken: cancellationToken) 
               ?? new Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets.CreateMcpSecretResponse();
    }

    public async Task DeleteSecretAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await SendDeleteRequestMessage($"/api/mcp-config/secrets/{id}");
        response.EnsureSuccessStatusCode();
    }
}
