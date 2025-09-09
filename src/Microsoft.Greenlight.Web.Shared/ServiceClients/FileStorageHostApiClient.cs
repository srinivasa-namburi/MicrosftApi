// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client implementation for managing file storage hosts.
/// </summary>
public class FileStorageHostApiClient : CrossPlatformServiceClientBase<FileStorageHostApiClient>, IFileStorageHostApiClient, IServiceClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageHostApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="authStateProvider">The authentication state provider.</param>
    public FileStorageHostApiClient(HttpClient httpClient, ILogger<FileStorageHostApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    /// <inheritdoc />
    public async Task<List<FileStorageHostInfo>> GetAllFileStorageHostsAsync()
    {
        var response = await SendGetRequestMessage("/api/file-storage-hosts", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<FileStorageHostInfo>>()! ?? new List<FileStorageHostInfo>();
    }

    /// <inheritdoc />
    public async Task<FileStorageHostInfo?> GetFileStorageHostByIdAsync(Guid id)
    {
        try
        {
            var response = await SendGetRequestMessage($"/api/file-storage-hosts/{id}", authorize: true);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<FileStorageHostInfo>()!;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<FileStorageHostInfo> CreateFileStorageHostAsync(CreateFileStorageHostRequest request)
    {
        var response = await SendPostRequestMessage("/api/file-storage-hosts", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var created = await response?.Content.ReadFromJsonAsync<FileStorageHostInfo>()!;
        return created ?? throw new InvalidOperationException("Failed to deserialize created file storage host.");
    }

    /// <inheritdoc />
    public async Task<FileStorageHostInfo> UpdateFileStorageHostAsync(UpdateFileStorageHostRequest request)
    {
        var response = await SendPutRequestMessage($"/api/file-storage-hosts/{request.Id}", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var updated = await response?.Content.ReadFromJsonAsync<FileStorageHostInfo>()!;
        return updated ?? throw new InvalidOperationException("Failed to deserialize updated file storage host.");
    }

    /// <inheritdoc />
    public async Task DeleteFileStorageHostAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/file-storage-hosts/{id}", authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<bool> TestFileStorageHostConnectionAsync(Guid id)
    {
        try
        {
            var response = await SendPostRequestMessage($"/api/file-storage-hosts/{id}/test-connection", null, authorize: true);
            return response?.IsSuccessStatusCode ?? false;
        }
        catch
        {
            return false;
        }
    }
}