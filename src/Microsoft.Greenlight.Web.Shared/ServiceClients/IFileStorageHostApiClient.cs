// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client interface for managing file storage hosts.
/// </summary>
public interface IFileStorageHostApiClient
{
    /// <summary>
    /// Gets all file storage hosts.
    /// </summary>
    /// <returns>A list of file storage host information.</returns>
    Task<List<FileStorageHostInfo>> GetAllFileStorageHostsAsync();

    /// <summary>
    /// Gets a specific file storage host by ID.
    /// </summary>
    /// <param name="id">The ID of the file storage host.</param>
    /// <returns>The file storage host information if found.</returns>
    Task<FileStorageHostInfo?> GetFileStorageHostByIdAsync(Guid id);

    /// <summary>
    /// Creates a new file storage host.
    /// </summary>
    /// <param name="request">The request containing file storage host information to create.</param>
    /// <returns>The created file storage host information.</returns>
    Task<FileStorageHostInfo> CreateFileStorageHostAsync(CreateFileStorageHostRequest request);

    /// <summary>
    /// Updates an existing file storage host.
    /// </summary>
    /// <param name="request">The request containing updated file storage host information.</param>
    /// <returns>The updated file storage host information.</returns>
    Task<FileStorageHostInfo> UpdateFileStorageHostAsync(UpdateFileStorageHostRequest request);

    /// <summary>
    /// Deletes a file storage host.
    /// </summary>
    /// <param name="id">The ID of the file storage host to delete.</param>
    /// <returns>A task representing the operation.</returns>
    Task DeleteFileStorageHostAsync(Guid id);

    /// <summary>
    /// Tests the connection to a file storage host.
    /// </summary>
    /// <param name="id">The ID of the file storage host to test.</param>
    /// <returns>True if the connection is successful, false otherwise.</returns>
    Task<bool> TestFileStorageHostConnectionAsync(Guid id);
}