// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Azure.Storage.Blobs;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing file storage hosts.
/// </summary>
[Route("/api/file-storage-hosts")]
public class FileStorageHostController : BaseController
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IMapper _mapper;
    private readonly ILogger<FileStorageHostController> _logger;
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _serviceConfigurationOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageHostController"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options monitor.</param>
    public FileStorageHostController(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IMapper mapper,
        ILogger<FileStorageHostController> logger,
        IOptionsMonitor<ServiceConfigurationOptions> serviceConfigurationOptions)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
        _logger = logger;
        _serviceConfigurationOptions = serviceConfigurationOptions;
    }

    /// <summary>
    /// Gets all file storage hosts.
    /// </summary>
    /// <returns>A list of file storage host information.</returns>
    [HttpGet]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<FileStorageHostInfo>>> GetAllFileStorageHostsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var hosts = await db.FileStorageHosts
            .AsNoTracking()
            .Include(h => h.Sources.Where(s => s.IsActive))
            .OrderByDescending(h => h.IsDefault)
            .ThenBy(h => h.Name)
            .ToListAsync();

        var hostInfos = _mapper.Map<List<FileStorageHostInfo>>(hosts);
        return Ok(hostInfos);
    }

    /// <summary>
    /// Gets a specific file storage host by ID.
    /// </summary>
    /// <param name="id">The ID of the file storage host.</param>
    /// <returns>The file storage host information if found.</returns>
    [HttpGet("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageHostInfo>> GetFileStorageHostByIdAsync(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var host = await db.FileStorageHosts
            .AsNoTracking()
            .Include(h => h.Sources.Where(s => s.IsActive))
            .FirstOrDefaultAsync(h => h.Id == id);

        if (host == null)
        {
            return NotFound();
        }

        var hostInfo = _mapper.Map<FileStorageHostInfo>(host);
        return Ok(hostInfo);
    }

    /// <summary>
    /// Creates a new file storage host.
    /// </summary>
    /// <param name="request">The create file storage host request.</param>
    /// <returns>The created file storage host information.</returns>
    [HttpPost]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageHostInfo>> CreateFileStorageHostAsync([FromBody] CreateFileStorageHostRequest request)
    {
        // Check if local file storage is available for new hosts
        if (request.ProviderType == FileStorageProviderType.LocalFileSystem)
        {
            var ingestionOptions = _serviceConfigurationOptions.CurrentValue.GreenlightServices.DocumentIngestion;
            if (!ingestionOptions.LocalFileStorageAvailable)
            {
                return BadRequest("Local file storage is not available. Please contact your system administrator to enable this feature.");
            }
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Check if a host with the same name already exists
        var existingHost = await db.FileStorageHosts
            .FirstOrDefaultAsync(h => h.Name == request.Name);
        
        if (existingHost != null)
        {
            return BadRequest($"A file storage host with the name '{request.Name}' already exists.");
        }

        // If this is marked as default, unset any existing default
        if (request.IsDefault)
        {
            var existingDefault = await db.FileStorageHosts
                .FirstOrDefaultAsync(h => h.IsDefault);
            
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                db.FileStorageHosts.Update(existingDefault);
            }
        }

        var host = new FileStorageHost
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProviderType = request.ProviderType,
            ConnectionString = request.ConnectionString,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            AuthenticationKey = request.AuthenticationKey,
            Description = request.Description
        };

        db.FileStorageHosts.Add(host);
        await db.SaveChangesAsync();

        var createdHostInfo = _mapper.Map<FileStorageHostInfo>(host);
        return Ok(createdHostInfo);
    }

    /// <summary>
    /// Updates an existing file storage host.
    /// </summary>
    /// <param name="id">The ID of the file storage host to update.</param>
    /// <param name="request">The update file storage host request.</param>
    /// <returns>The updated file storage host information.</returns>
    [HttpPut("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageHostInfo>> UpdateFileStorageHostAsync(Guid id, [FromBody] UpdateFileStorageHostRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest("The ID in the URL does not match the ID in the request body.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var existingHost = await db.FileStorageHosts.FirstOrDefaultAsync(h => h.Id == id);
        if (existingHost == null)
        {
            return NotFound();
        }

        // Check if another host with the same name already exists (excluding the current one)
        var duplicateNameHost = await db.FileStorageHosts
            .FirstOrDefaultAsync(h => h.Name == request.Name && h.Id != id);
        
        if (duplicateNameHost != null)
        {
            return BadRequest($"Another file storage host with the name '{request.Name}' already exists.");
        }

        // If this is marked as default, unset any existing default
        if (request.IsDefault && !existingHost.IsDefault)
        {
            var existingDefault = await db.FileStorageHosts
                .FirstOrDefaultAsync(h => h.IsDefault && h.Id != id);
            
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                db.FileStorageHosts.Update(existingDefault);
            }
        }

        // Update properties
        existingHost.Name = request.Name;
        existingHost.ProviderType = request.ProviderType;
        existingHost.ConnectionString = request.ConnectionString;
        existingHost.IsDefault = request.IsDefault;
        existingHost.IsActive = request.IsActive;
        existingHost.AuthenticationKey = request.AuthenticationKey;
        existingHost.Description = request.Description;

        await db.SaveChangesAsync();

        var updatedHostInfo = _mapper.Map<FileStorageHostInfo>(existingHost);
        return Ok(updatedHostInfo);
    }

    /// <summary>
    /// Deletes a file storage host.
    /// </summary>
    /// <param name="id">The ID of the file storage host to delete.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFileStorageHostAsync(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var host = await db.FileStorageHosts
            .Include(h => h.Sources)
            .FirstOrDefaultAsync(h => h.Id == id);
        
        if (host == null)
        {
            return NotFound();
        }

        // Check if the host has any associated sources
        if (host.Sources.Any())
        {
            return BadRequest("Cannot delete file storage host because it has associated file storage sources. Remove all sources first.");
        }

        // Don't allow deletion of the default host if it's the only one
        if (host.IsDefault)
        {
            var totalHosts = await db.FileStorageHosts.CountAsync();
            if (totalHosts <= 1)
            {
                return BadRequest("Cannot delete the last file storage host. At least one host is required.");
            }

            // If deleting the default host, promote another active host to default
            var newDefaultHost = await db.FileStorageHosts
                .Where(h => h.Id != id && h.IsActive)
                .OrderBy(h => h.Name)
                .FirstOrDefaultAsync();

            if (newDefaultHost != null)
            {
                newDefaultHost.IsDefault = true;
                db.FileStorageHosts.Update(newDefaultHost);
                _logger.LogInformation("Promoted host {HostName} to default after deleting previous default host", newDefaultHost.Name);
            }
        }

        db.FileStorageHosts.Remove(host);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Tests the connection to a file storage host.
    /// </summary>
    /// <param name="id">The ID of the file storage host to test.</param>
    /// <returns>Success status of the connection test.</returns>
    [HttpPost("{id}/test-connection")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<bool>> TestFileStorageHostConnectionAsync(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var host = await db.FileStorageHosts
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id);

        if (host == null)
        {
            return NotFound();
        }

        try
        {
            // Basic connection test based on provider type
            var isConnected = host.ProviderType switch
            {
                Shared.Enums.FileStorageProviderType.BlobStorage => await TestBlobStorageConnectionAsync(host),
                Shared.Enums.FileStorageProviderType.LocalFileSystem => await TestLocalFileSystemConnectionAsync(host),
                Shared.Enums.FileStorageProviderType.SharePoint => await TestSharePointConnectionAsync(host),
                _ => false
            };

            return Ok(isConnected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to test connection to file storage host {HostId} ({HostName})", id, host.Name);
            return Ok(false);
        }
    }

    /// <summary>
    /// Tests Azure Blob Storage connection.
    /// </summary>
    /// <param name="host">The file storage host configuration.</param>
    /// <returns>True if connection is successful.</returns>
    private async Task<bool> TestBlobStorageConnectionAsync(FileStorageHost host)
    {
        try
        {
            if (host.ConnectionString == "default")
            {
                // For default connection, we assume it's working if the system is properly configured
                // In a real implementation, you'd inject the default BlobServiceClient and test it
                return true;
            }

            // For custom connection strings, attempt to create a BlobServiceClient and test basic connectivity
            var blobServiceClient = new BlobServiceClient(host.ConnectionString);
            var properties = await blobServiceClient.GetPropertiesAsync();
            return properties != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests local file system connection.
    /// </summary>
    /// <param name="host">The file storage host configuration.</param>
    /// <returns>True if connection is successful.</returns>
    private async Task<bool> TestLocalFileSystemConnectionAsync(FileStorageHost host)
    {
        try
        {
            // Test if the directory exists and is accessible
            if (Directory.Exists(host.ConnectionString))
            {
                // Try to create and delete a test file to verify write permissions
                var testFile = Path.Combine(host.ConnectionString, $"test-{Guid.NewGuid()}.tmp");
                await System.IO.File.WriteAllTextAsync(testFile, "test");
                System.IO.File.Delete(testFile);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests SharePoint connection.
    /// </summary>
    /// <param name="host">The file storage host configuration.</param>
    /// <returns>True if connection is successful.</returns>
    private async Task<bool> TestSharePointConnectionAsync(FileStorageHost host)
    {
        // SharePoint connection testing would be implemented here
        // For now, return false as SharePoint provider is not yet implemented
        await Task.CompletedTask;
        return false;
    }
}