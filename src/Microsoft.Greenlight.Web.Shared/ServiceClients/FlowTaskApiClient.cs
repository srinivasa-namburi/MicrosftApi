// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client for Flow Task template operations.
/// </summary>
public class FlowTaskApiClient : CrossPlatformServiceClientBase<FlowTaskApiClient>, IFlowTaskApiClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="authStateProvider">The authentication state provider.</param>
    public FlowTaskApiClient(
        HttpClient httpClient,
        ILogger<FlowTaskApiClient> logger,
        AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    /// <inheritdoc/>
    public async Task<List<FlowTaskTemplateInfo>> GetAllFlowTaskTemplatesAsync(
        string? category = null,
        bool? isActive = null,
        int skip = 0,
        int take = 100)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
        {
            queryParams.Add($"category={Uri.EscapeDataString(category)}");
        }
        if (isActive.HasValue)
        {
            queryParams.Add($"isActive={isActive.Value}");
        }
        queryParams.Add($"skip={skip}");
        queryParams.Add($"take={take}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var response = await SendGetRequestMessage($"/api/flow-tasks{queryString}", authorize: true);
        response?.EnsureSuccessStatusCode();
        var templates = await response!.Content.ReadFromJsonAsync<List<FlowTaskTemplateInfo>>();
        return templates ?? new List<FlowTaskTemplateInfo>();
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateDetailDto?> GetFlowTaskTemplateByIdAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/flow-tasks/{id}", authorize: true);
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<FlowTaskTemplateDetailDto>();
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetCategoriesAsync()
    {
        var response = await SendGetRequestMessage("/api/flow-tasks/categories", authorize: true);
        response?.EnsureSuccessStatusCode();
        var categories = await response!.Content.ReadFromJsonAsync<List<string>>();
        return categories ?? new List<string>();
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateDetailDto> CreateFlowTaskTemplateAsync(FlowTaskTemplateDetailDto template)
    {
        var response = await SendPostRequestMessage("/api/flow-tasks", template, authorize: true);
        response?.EnsureSuccessStatusCode();
        var created = await response!.Content.ReadFromJsonAsync<FlowTaskTemplateDetailDto>();
        return created!;
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateDetailDto> UpdateFlowTaskTemplateAsync(Guid id, FlowTaskTemplateDetailDto template)
    {
        var response = await SendPutRequestMessage($"/api/flow-tasks/{id}", template, authorize: true);
        response?.EnsureSuccessStatusCode();
        var updated = await response!.Content.ReadFromJsonAsync<FlowTaskTemplateDetailDto>();
        return updated!;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteFlowTaskTemplateAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/flow-tasks/{id}", authorize: true);
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        response?.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateDetailDto> ImportFlowTaskTemplateAsync(FlowTaskTemplateDetailDto template)
    {
        var response = await SendPostRequestMessage("/api/flow-tasks/import", template, authorize: true);
        response?.EnsureSuccessStatusCode();
        var imported = await response!.Content.ReadFromJsonAsync<FlowTaskTemplateDetailDto>();
        return imported!;
    }

    /// <inheritdoc/>
    public async Task<FlowTaskTemplateDetailDto?> ExportFlowTaskTemplateAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/flow-tasks/{id}/export", authorize: true);
        if (response?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<FlowTaskTemplateDetailDto>();
    }
}
