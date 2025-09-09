// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;
using System.Net.Http.Json;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client implementation for managing file storage sources.
/// </summary>
public class FileStorageSourceApiClient : CrossPlatformServiceClientBase<FileStorageSourceApiClient>, IFileStorageSourceApiClient, IServiceClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageSourceApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="authStateProvider">The authentication state provider.</param>
    public FileStorageSourceApiClient(HttpClient httpClient, ILogger<FileStorageSourceApiClient> logger, AuthenticationStateProvider authStateProvider)
        : base(httpClient, logger, authStateProvider)
    {
    }

    /// <inheritdoc />
    public async Task<List<FileStorageSourceInfo>> GetAllFileStorageSourcesAsync()
    {
        var response = await SendGetRequestMessage("/api/file-storage-sources", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<FileStorageSourceInfo>>()! ?? new List<FileStorageSourceInfo>();
    }

    /// <inheritdoc />
    public async Task<FileStorageSourceInfo?> GetFileStorageSourceByIdAsync(Guid id)
    {
        try
        {
            var response = await SendGetRequestMessage($"/api/file-storage-sources/{id}", authorize: true);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<FileStorageSourceInfo>()!;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<FileStorageSourceInfo>> GetFileStorageSourcesByProcessIdAsync(Guid processId)
    {
        var response = await SendGetRequestMessage($"/api/file-storage-sources/document-process/{processId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<FileStorageSourceInfo>>()! ?? new List<FileStorageSourceInfo>();
    }

    /// <inheritdoc />
    public async Task<List<FileStorageSourceInfo>> GetFileStorageSourcesByLibraryIdAsync(Guid libraryId)
    {
        var response = await SendGetRequestMessage($"/api/file-storage-sources/document-library/{libraryId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<FileStorageSourceInfo>>()! ?? new List<FileStorageSourceInfo>();
    }

    /// <inheritdoc />
    public async Task<FileStorageSourceInfo> CreateFileStorageSourceAsync(CreateFileStorageSourceRequest request)
    {
        var response = await SendPostRequestMessage("/api/file-storage-sources", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var created = await response?.Content.ReadFromJsonAsync<FileStorageSourceInfo>()!;
        return created ?? throw new InvalidOperationException("Failed to deserialize created file storage source.");
    }

    /// <inheritdoc />
    public async Task<FileStorageSourceInfo> UpdateFileStorageSourceAsync(UpdateFileStorageSourceRequest request)
    {
        var response = await SendPutRequestMessage($"/api/file-storage-sources/{request.Id}", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var updated = await response?.Content.ReadFromJsonAsync<FileStorageSourceInfo>()!;
        return updated ?? throw new InvalidOperationException("Failed to deserialize updated file storage source.");
    }

    /// <inheritdoc />
    public async Task DeleteFileStorageSourceAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/file-storage-sources/{id}", authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task AssociateSourceWithProcessAsync(Guid processId, Guid sourceId)
    {
        var response = await SendPostRequestMessage($"/api/file-storage-sources/document-process/{processId}/sources/{sourceId}", null, authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DisassociateSourceFromProcessAsync(Guid processId, Guid sourceId)
    {
        var response = await SendDeleteRequestMessage($"/api/file-storage-sources/document-process/{processId}/sources/{sourceId}", authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task AssociateSourceWithLibraryAsync(Guid libraryId, Guid sourceId)
    {
        var response = await SendPostRequestMessage($"/api/file-storage-sources/document-library/{libraryId}/sources/{sourceId}", null, authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task DisassociateSourceFromLibraryAsync(Guid libraryId, Guid sourceId)
    {
        var response = await SendDeleteRequestMessage($"/api/file-storage-sources/document-library/{libraryId}/sources/{sourceId}", authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<List<DocumentProcessFileStorageSourceInfo>> GetFileStorageSourceAssociationsByProcessIdAsync(Guid processId)
    {
        var response = await SendGetRequestMessage($"/api/file-storage-sources/document-process/{processId}/associations", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<DocumentProcessFileStorageSourceInfo>>()! ?? new List<DocumentProcessFileStorageSourceInfo>();
    }

    /// <inheritdoc />
    public async Task<List<DocumentLibraryFileStorageSourceInfo>> GetFileStorageSourceAssociationsByLibraryIdAsync(Guid libraryId)
    {
        var response = await SendGetRequestMessage($"/api/file-storage-sources/document-library/{libraryId}/associations", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<DocumentLibraryFileStorageSourceInfo>>()! ?? new List<DocumentLibraryFileStorageSourceInfo>();
    }

    /// <inheritdoc />
    public async Task<DocumentProcessFileStorageSourceInfo> UpdateProcessSourceAssociationAsync(Guid processId, Guid sourceId, UpdateProcessSourceAssociationRequest request)
    {
        var response = await SendPutRequestMessage($"/api/file-storage-sources/document-process/{processId}/sources/{sourceId}/association", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var updated = await response?.Content.ReadFromJsonAsync<DocumentProcessFileStorageSourceInfo>()!;
        return updated ?? throw new InvalidOperationException("Failed to deserialize updated association.");
    }

    /// <inheritdoc />
    public async Task<DocumentLibraryFileStorageSourceInfo> UpdateLibrarySourceAssociationAsync(Guid libraryId, Guid sourceId, UpdateLibrarySourceAssociationRequest request)
    {
        var response = await SendPutRequestMessage($"/api/file-storage-sources/document-library/{libraryId}/sources/{sourceId}/association", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        
        var updated = await response?.Content.ReadFromJsonAsync<DocumentLibraryFileStorageSourceInfo>()!;
        return updated ?? throw new InvalidOperationException("Failed to deserialize updated association.");
    }

    /// <inheritdoc />
    public async Task<DocumentProcessFileStorageSourceInfo?> GetUploadSourceForProcessAsync(Guid processId)
    {
        try
        {
            var response = await SendGetRequestMessage($"/api/file-storage-sources/document-process/{processId}/upload-source", authorize: true);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DocumentProcessFileStorageSourceInfo>()!;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<DocumentLibraryFileStorageSourceInfo?> GetUploadSourceForLibraryAsync(Guid libraryId)
    {
        try
        {
            var response = await SendGetRequestMessage($"/api/file-storage-sources/document-library/{libraryId}/upload-source", authorize: true);
            response?.EnsureSuccessStatusCode();
            return await response?.Content.ReadFromJsonAsync<DocumentLibraryFileStorageSourceInfo>()!;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    // ContentReferenceType ↔ FileStorageSource mappings
    public async Task<List<ContentReferenceTypeStorageSourceMappingInfo>> GetAllContentReferenceTypeMappingsAsync()
    {
        var response = await SendGetRequestMessage("/api/file-storage-sources/content-reference-type/mappings", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<List<ContentReferenceTypeStorageSourceMappingInfo>>() ?? new();
    }

    public async Task<List<ContentReferenceTypeStorageSourceMappingInfo>> GetContentReferenceTypeMappingsAsync(ContentReferenceType type)
    {
        var response = await SendGetRequestMessage($"/api/file-storage-sources/content-reference-type/{type}/mappings", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<List<ContentReferenceTypeStorageSourceMappingInfo>>() ?? new();
    }

    public async Task<ContentReferenceTypeStorageSourceMappingInfo> CreateContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId)
    {
        var response = await SendPostRequestMessage($"/api/file-storage-sources/content-reference-type/{type}/sources/{sourceId}", null, authorize: true);
        response?.EnsureSuccessStatusCode();
        return (await response!.Content.ReadFromJsonAsync<ContentReferenceTypeStorageSourceMappingInfo>())!;
    }

    public async Task<ContentReferenceTypeStorageSourceMappingInfo> UpdateContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId, int priority, bool isActive, bool acceptsUploads)
    {
        var body = new { Priority = priority, IsActive = isActive, AcceptsUploads = acceptsUploads };
        // Send object directly (serialization handled in base helper)
        var response = await SendPutRequestMessage($"/api/file-storage-sources/content-reference-type/{type}/sources/{sourceId}", body, authorize: true);
        response?.EnsureSuccessStatusCode();
        return (await response!.Content.ReadFromJsonAsync<ContentReferenceTypeStorageSourceMappingInfo>())!;
    }

    public async Task DeleteContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId)
    {
        var response = await SendDeleteRequestMessage($"/api/file-storage-sources/content-reference-type/{type}/sources/{sourceId}", authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    #region Legacy method overloads for backward compatibility

    /// <inheritdoc />
    [Obsolete("Use CreateFileStorageSourceAsync(CreateFileStorageSourceRequest) instead")]
    public async Task<FileStorageSourceInfo> CreateFileStorageSourceAsync(FileStorageSourceInfo sourceInfo)
    {
        // Convert legacy format to new request format
        var request = new CreateFileStorageSourceRequest
        {
            Name = sourceInfo.Name,
            FileStorageHostId = sourceInfo.FileStorageHostId,
            ContainerOrPath = sourceInfo.ContainerOrPath,
            AutoImportFolderName = sourceInfo.AutoImportFolderName,
            IsDefault = sourceInfo.IsDefault,
            IsActive = sourceInfo.IsActive,
            ShouldMoveFiles = sourceInfo.ShouldMoveFiles,
            Description = sourceInfo.Description
        };

        return await CreateFileStorageSourceAsync(request);
    }

    /// <inheritdoc />
    [Obsolete("Use UpdateFileStorageSourceAsync(UpdateFileStorageSourceRequest) instead")]
    public async Task<FileStorageSourceInfo> UpdateFileStorageSourceAsync(Guid id, FileStorageSourceInfo sourceInfo)
    {
        // Convert legacy format to new request format
        var request = new UpdateFileStorageSourceRequest
        {
            Id = id,
            Name = sourceInfo.Name,
            FileStorageHostId = sourceInfo.FileStorageHostId,
            ContainerOrPath = sourceInfo.ContainerOrPath,
            AutoImportFolderName = sourceInfo.AutoImportFolderName,
            IsDefault = sourceInfo.IsDefault,
            IsActive = sourceInfo.IsActive,
            ShouldMoveFiles = sourceInfo.ShouldMoveFiles,
            Description = sourceInfo.Description
        };

        return await UpdateFileStorageSourceAsync(request);
    }

    #endregion
}
