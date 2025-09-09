// Copyright (c) Microsoft Corporation. All rights reserved.

using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Factory for creating file storage service instances based on configuration.
/// </summary>
public class FileStorageServiceFactory : IFileStorageServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileStorageServiceFactory> _logger;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageServiceFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dbContextFactory">Database context factory.</param>
    /// <param name="documentProcessInfoService">Document process info service.</param>
    /// <param name="documentLibraryInfoService">Document library info service.</param>
    public FileStorageServiceFactory(
        IServiceProvider serviceProvider,
        ILogger<FileStorageServiceFactory> logger,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
    }

    /// <summary>
    /// Creates a file storage service instance for the specified source.
    /// </summary>
    /// <param name="sourceInfo">File storage source configuration.</param>
    /// <returns>Configured file storage service instance.</returns>
    public IFileStorageService CreateService(FileStorageSourceInfo sourceInfo)
    {
        return sourceInfo.ProviderType switch
        {
            FileStorageProviderType.BlobStorage => CreateBlobStorageService(sourceInfo),
            FileStorageProviderType.LocalFileSystem => CreateLocalFileSystemService(sourceInfo),
            FileStorageProviderType.SharePoint => throw new NotImplementedException("SharePoint provider is not yet implemented"),
            _ => throw new ArgumentException($"Unknown file storage provider type: {sourceInfo.ProviderType}")
        };
    }

    /// <summary>
    /// Gets the default file storage service (first configured source).
    /// </summary>
    /// <returns>Default file storage service instance.</returns>
    public IFileStorageService GetDefaultService()
    {
        // Create a default blob storage service for backward compatibility
        var defaultBlobServiceClient = _serviceProvider.GetRequiredKeyedService<BlobServiceClient>("blob-docing");
        var logger = _serviceProvider.GetRequiredService<ILogger<BlobStorageFileStorageService>>();
        var dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();

        var defaultHostInfo = new FileStorageHostInfo
        {
            Id = Guid.Empty,
            Name = "Default Blob Storage Host",
            ProviderType = FileStorageProviderType.BlobStorage,
            ConnectionString = "default",
            IsDefault = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        var defaultSourceInfo = new FileStorageSourceInfo
        {
            Id = Guid.Empty,
            Name = "Default Blob Storage",
            FileStorageHostId = Guid.Empty,
            FileStorageHost = defaultHostInfo,
            ContainerOrPath = "default-container",
            AutoImportFolderName = "ingest-auto",
            IsDefault = true,
            IsActive = true,
            ShouldMoveFiles = true, // Default blob storage uses move behavior for backward compatibility
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        return new BlobStorageFileStorageService(defaultBlobServiceClient, logger, defaultSourceInfo, dbContextFactory);
    }

    /// <summary>
    /// Gets all available file storage services for a document process/library.
    /// </summary>
    /// <param name="documentProcessOrLibraryName">Name of the document process or library.</param>
    /// <param name="isDocumentLibrary">True for document library, false for document process.</param>
    /// <returns>Collection of file storage services.</returns>
    public async Task<IEnumerable<IFileStorageService>> GetServicesForDocumentProcessOrLibraryAsync(string documentProcessOrLibraryName, bool isDocumentLibrary)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            List<FileStorageSourceInfo> sources;

            if (isDocumentLibrary)
            {
                // Get document library and its associated file storage sources
                var library = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentProcessOrLibraryName);
                if (library == null)
                {
                    _logger.LogWarning("Document library {LibraryName} not found", documentProcessOrLibraryName);
                    return Enumerable.Empty<IFileStorageService>();
                }

                sources = await db.DocumentLibraryFileStorageSources
                    .Include(x => x.DocumentLibrary)
                    .Where(x => x.DocumentLibrary.ShortName == documentProcessOrLibraryName && x.IsActive)
                    .Include(x => x.FileStorageSource)
                    .ThenInclude(x => x.FileStorageHost)
                    .OrderBy(x => x.Priority)
                    .Select(x => new FileStorageSourceInfo
                    {
                        Id = x.FileStorageSource.Id,
                        Name = x.FileStorageSource.Name,
                        FileStorageHostId = x.FileStorageSource.FileStorageHostId,
                        FileStorageHost = new FileStorageHostInfo
                        {
                            Id = x.FileStorageSource.FileStorageHost.Id,
                            Name = x.FileStorageSource.FileStorageHost.Name,
                            ProviderType = x.FileStorageSource.FileStorageHost.ProviderType,
                            ConnectionString = x.FileStorageSource.FileStorageHost.ConnectionString,
                            IsDefault = x.FileStorageSource.FileStorageHost.IsDefault,
                            IsActive = x.FileStorageSource.FileStorageHost.IsActive,
                            AuthenticationKey = x.FileStorageSource.FileStorageHost.AuthenticationKey,
                            Description = x.FileStorageSource.FileStorageHost.Description,
                            CreatedDate = x.FileStorageSource.FileStorageHost.CreatedUtc,
                            LastUpdatedDate = x.FileStorageSource.FileStorageHost.ModifiedUtc
                        },
                        ContainerOrPath = x.FileStorageSource.ContainerOrPath,
                        AutoImportFolderName = x.FileStorageSource.AutoImportFolderName,
                        IsDefault = x.FileStorageSource.IsDefault,
                        IsActive = x.FileStorageSource.IsActive,
                        ShouldMoveFiles = x.FileStorageSource.ShouldMoveFiles,
                        Description = x.FileStorageSource.Description,
                        CreatedDate = x.FileStorageSource.CreatedUtc,
                        LastUpdatedDate = x.FileStorageSource.ModifiedUtc
                    })
                    .ToListAsync();
            }
            else
            {
                // Get document process and its associated file storage sources
                var process = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessOrLibraryName);
                if (process == null)
                {
                    _logger.LogWarning("Document process {ProcessName} not found", documentProcessOrLibraryName);
                    return Enumerable.Empty<IFileStorageService>();
                }

                sources = await db.DocumentProcessFileStorageSources
                    .Include(x => x.DocumentProcess)
                    .Where(x => x.DocumentProcess.ShortName == documentProcessOrLibraryName && x.IsActive)
                    .Include(x => x.FileStorageSource)
                    .ThenInclude(x => x.FileStorageHost)
                    .OrderBy(x => x.Priority)
                    .Select(x => new FileStorageSourceInfo
                    {
                        Id = x.FileStorageSource.Id,
                        Name = x.FileStorageSource.Name,
                        FileStorageHostId = x.FileStorageSource.FileStorageHostId,
                        FileStorageHost = new FileStorageHostInfo
                        {
                            Id = x.FileStorageSource.FileStorageHost.Id,
                            Name = x.FileStorageSource.FileStorageHost.Name,
                            ProviderType = x.FileStorageSource.FileStorageHost.ProviderType,
                            ConnectionString = x.FileStorageSource.FileStorageHost.ConnectionString,
                            IsDefault = x.FileStorageSource.FileStorageHost.IsDefault,
                            IsActive = x.FileStorageSource.FileStorageHost.IsActive,
                            AuthenticationKey = x.FileStorageSource.FileStorageHost.AuthenticationKey,
                            Description = x.FileStorageSource.FileStorageHost.Description,
                            CreatedDate = x.FileStorageSource.FileStorageHost.CreatedUtc,
                            LastUpdatedDate = x.FileStorageSource.FileStorageHost.ModifiedUtc
                        },
                        ContainerOrPath = x.FileStorageSource.ContainerOrPath,
                        AutoImportFolderName = x.FileStorageSource.AutoImportFolderName,
                        IsDefault = x.FileStorageSource.IsDefault,
                        IsActive = x.FileStorageSource.IsActive,
                        ShouldMoveFiles = x.FileStorageSource.ShouldMoveFiles,
                        Description = x.FileStorageSource.Description,
                        CreatedDate = x.FileStorageSource.CreatedUtc,
                        LastUpdatedDate = x.FileStorageSource.ModifiedUtc
                    })
                    .ToListAsync();
            }

            // If no specific sources configured, fall back to backward compatibility mode
            if (!sources.Any())
            {
                _logger.LogInformation("No file storage sources configured for {Type} {Name}, using backward compatibility mode", 
                    isDocumentLibrary ? "library" : "process", documentProcessOrLibraryName);
                
                return new[] { CreateBackwardCompatibilityService(documentProcessOrLibraryName, isDocumentLibrary) };
            }

            return sources.Select(CreateService).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file storage services for {Type} {Name}", 
                isDocumentLibrary ? "library" : "process", documentProcessOrLibraryName);
            throw;
        }
    }

    /// <summary>
    /// Creates a blob storage service instance.
    /// </summary>
    /// <param name="sourceInfo">Source configuration.</param>
    /// <returns>Blob storage service instance.</returns>
    private IFileStorageService CreateBlobStorageService(FileStorageSourceInfo sourceInfo)
    {
        BlobServiceClient blobServiceClient;

        var connectionString = sourceInfo.FileStorageHost?.ConnectionString ?? sourceInfo.ConnectionString;

        // If connection string is "default" or empty, use the default keyed service
        if (string.IsNullOrEmpty(connectionString) || connectionString == "default")
        {
            blobServiceClient = _serviceProvider.GetRequiredKeyedService<BlobServiceClient>("blob-docing");
        }
        else
        {
            // Create a new BlobServiceClient with the specific connection string
            blobServiceClient = new BlobServiceClient(connectionString);
        }

        var logger = _serviceProvider.GetRequiredService<ILogger<BlobStorageFileStorageService>>();
        var dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();
        return new BlobStorageFileStorageService(blobServiceClient, logger, sourceInfo, dbContextFactory);
    }

    /// <summary>
    /// Creates a local file system service instance.
    /// </summary>
    /// <param name="sourceInfo">Source configuration.</param>
    /// <returns>Local file system service instance.</returns>
    private IFileStorageService CreateLocalFileSystemService(FileStorageSourceInfo sourceInfo)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<LocalFileStorageService>>();
        return new LocalFileStorageService(logger, sourceInfo, _dbContextFactory);
    }

    /// <summary>
    /// Creates a backward compatibility service for existing document processes/libraries.
    /// </summary>
    /// <param name="documentProcessOrLibraryName">Name of the document process or library.</param>
    /// <param name="isDocumentLibrary">True for document library, false for document process.</param>
    /// <returns>Backward compatibility service instance.</returns>
    private IFileStorageService CreateBackwardCompatibilityService(string documentProcessOrLibraryName, bool isDocumentLibrary)
    {
        // Get the blob storage container name from the existing configuration
        string containerName;
        string autoImportFolder;

        if (isDocumentLibrary)
        {
            var library = _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentProcessOrLibraryName).Result;
            containerName = library?.BlobStorageContainerName ?? "default-container";
            autoImportFolder = library?.BlobStorageAutoImportFolderName ?? "ingest-auto";
        }
        else
        {
            var process = _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessOrLibraryName).Result;
            containerName = process?.BlobStorageContainerName ?? "default-container";
            autoImportFolder = process?.BlobStorageAutoImportFolderName ?? "ingest-auto";
        }

        var backwardCompatibilityHostInfo = new FileStorageHostInfo
        {
            Id = Guid.Empty,
            Name = $"Backward Compatibility Host - {documentProcessOrLibraryName}",
            ProviderType = FileStorageProviderType.BlobStorage,
            ConnectionString = "default",
            IsDefault = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        var backwardCompatibilitySourceInfo = new FileStorageSourceInfo
        {
            Id = Guid.Empty,
            Name = $"Backward Compatibility - {documentProcessOrLibraryName}",
            FileStorageHostId = Guid.Empty,
            FileStorageHost = backwardCompatibilityHostInfo,
            ContainerOrPath = containerName,
            AutoImportFolderName = autoImportFolder,
            IsDefault = true,
            IsActive = true,
            ShouldMoveFiles = true, // Legacy behavior: move files
            CreatedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        return CreateBlobStorageService(backwardCompatibilitySourceInfo);
    }

    /// <summary>
    /// Gets a file storage service by its source ID.
    /// </summary>
    /// <param name="sourceId">The FileStorageSource ID.</param>
    /// <returns>File storage service instance, or null if not found.</returns>
    public async Task<IFileStorageService?> GetServiceBySourceIdAsync(Guid sourceId)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var sourceInfo = await db.FileStorageSources
                .Include(x => x.FileStorageHost)
                .Where(x => x.Id == sourceId && x.IsActive)
                .Select(x => new FileStorageSourceInfo
                {
                    Id = x.Id,
                    Name = x.Name,
                    FileStorageHostId = x.FileStorageHostId,
                    FileStorageHost = new FileStorageHostInfo
                    {
                        Id = x.FileStorageHost.Id,
                        Name = x.FileStorageHost.Name,
                        ProviderType = x.FileStorageHost.ProviderType,
                        ConnectionString = x.FileStorageHost.ConnectionString,
                        IsDefault = x.FileStorageHost.IsDefault,
                        IsActive = x.FileStorageHost.IsActive,
                        CreatedDate = x.FileStorageHost.CreatedUtc,
                        LastUpdatedDate = x.FileStorageHost.ModifiedUtc
                    },
                    ContainerOrPath = x.ContainerOrPath,
                    AutoImportFolderName = x.AutoImportFolderName,
                    IsDefault = x.IsDefault,
                    IsActive = x.IsActive,
                    ShouldMoveFiles = x.ShouldMoveFiles,
                    CreatedDate = x.CreatedUtc,
                    LastUpdatedDate = x.ModifiedUtc
                })
                .FirstOrDefaultAsync();

            if (sourceInfo == null)
            {
                _logger.LogWarning("FileStorageSource {SourceId} not found or inactive", sourceId);
                return null;
            }

            return CreateService(sourceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file storage service for source {SourceId}", sourceId);
            return null;
        }
    }
}