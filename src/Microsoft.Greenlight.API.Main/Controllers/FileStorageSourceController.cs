// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for managing file storage sources.
/// </summary>
[Route("/api/file-storage-sources")]
public class FileStorageSourceController : BaseController
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IMapper _mapper;
    private readonly ILogger<FileStorageSourceController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageSourceController"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The database context factory.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="logger">The logger instance.</param>
    public FileStorageSourceController(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IMapper mapper,
        ILogger<FileStorageSourceController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Gets all file storage sources.
    /// </summary>
    /// <returns>A list of file storage source information.</returns>
    [HttpGet]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<FileStorageSourceInfo>>> GetAllFileStorageSourcesAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var sources = await db.FileStorageSources
            .AsNoTracking()
            .Include(s => s.FileStorageHost)
            .Include(s => s.Categories)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var sourceInfos = _mapper.Map<List<FileStorageSourceInfo>>(sources);
        return Ok(sourceInfos);
    }

    /// <summary>
    /// Gets a specific file storage source by ID.
    /// </summary>
    /// <param name="id">The ID of the file storage source.</param>
    /// <returns>The file storage source information if found.</returns>
    [HttpGet("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageSourceInfo>> GetFileStorageSourceByIdAsync(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var source = await db.FileStorageSources
            .AsNoTracking()
            .Include(s => s.FileStorageHost)
            .Include(s => s.Categories)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
        {
            return NotFound();
        }

        var sourceInfo = _mapper.Map<FileStorageSourceInfo>(source);
        return Ok(sourceInfo);
    }

    /// <summary>
    /// Gets file storage sources for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>A list of file storage sources associated with the process.</returns>
    [HttpGet("document-process/{processId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<FileStorageSourceInfo>>> GetFileStorageSourcesByProcessIdAsync(Guid processId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var sources = await db.DocumentProcessFileStorageSources
            .AsNoTracking()
            .Where(dps => dps.DocumentProcessId == processId)
            .Include(dps => dps.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Select(dps => dps.FileStorageSource)
            .ToListAsync();

        var sourceInfos = _mapper.Map<List<FileStorageSourceInfo>>(sources);
        return Ok(sourceInfos);
    }

    /// <summary>
    /// Gets file storage sources for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>A list of file storage sources associated with the library.</returns>
    [HttpGet("document-library/{libraryId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<FileStorageSourceInfo>>> GetFileStorageSourcesByLibraryIdAsync(Guid libraryId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var sources = await db.DocumentLibraryFileStorageSources
            .AsNoTracking()
            .Where(dls => dls.DocumentLibraryId == libraryId)
            .Include(dls => dls.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Select(dls => dls.FileStorageSource)
            .ToListAsync();

        var sourceInfos = _mapper.Map<List<FileStorageSourceInfo>>(sources);
        return Ok(sourceInfos);
    }

    /// <summary>
    /// Creates a new file storage source.
    /// </summary>
    /// <param name="request">The create file storage source request.</param>
    /// <returns>The created file storage source information.</returns>
    [HttpPost]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageSourceInfo>> CreateFileStorageSourceAsync([FromBody] CreateFileStorageSourceRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Check if a source with the same name already exists
        var existingSource = await db.FileStorageSources
            .FirstOrDefaultAsync(s => s.Name == request.Name);
        
        if (existingSource != null)
        {
            return BadRequest($"A file storage source with the name '{request.Name}' already exists.");
        }

        // Verify the host exists
        var hostExists = await db.FileStorageHosts.AnyAsync(h => h.Id == request.FileStorageHostId);
        if (!hostExists)
        {
            return BadRequest("The specified file storage host does not exist.");
        }

        var source = new FileStorageSource
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            FileStorageHostId = request.FileStorageHostId,
            ContainerOrPath = request.ContainerOrPath,
            AutoImportFolderName = request.AutoImportFolderName,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            ShouldMoveFiles = request.ShouldMoveFiles,
            Description = request.Description
        };

        db.FileStorageSources.Add(source);
        await db.SaveChangesAsync();

        // Apply categories from request (if any)
        if (request.StorageSourceDataTypes?.Any() == true)
        {
            foreach (var dt in request.StorageSourceDataTypes.Distinct())
            {
                db.FileStorageSourceCategories.Add(new FileStorageSourceCategory
                {
                    Id = Guid.NewGuid(),
                    FileStorageSourceId = source.Id,
                    DataType = dt
                });
            }
            await db.SaveChangesAsync();
        }

        // Load the source with host information for mapping
        var createdSource = await db.FileStorageSources
            .Include(s => s.FileStorageHost)
            .Include(s => s.Categories)
            .FirstOrDefaultAsync(s => s.Id == source.Id);

        var createdSourceInfo = _mapper.Map<FileStorageSourceInfo>(createdSource);
        return Ok(createdSourceInfo);
    }

    /// <summary>
    /// Updates an existing file storage source.
    /// </summary>
    /// <param name="id">The ID of the file storage source to update.</param>
    /// <param name="request">The update file storage source request.</param>
    /// <returns>The updated file storage source information.</returns>
    [HttpPut("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<FileStorageSourceInfo>> UpdateFileStorageSourceAsync(Guid id, [FromBody] UpdateFileStorageSourceRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest("The ID in the URL does not match the ID in the request body.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var existingSource = await db.FileStorageSources.FirstOrDefaultAsync(s => s.Id == id);
        if (existingSource == null)
        {
            return NotFound();
        }

        // Check if another source with the same name already exists (excluding the current one)
        var duplicateNameSource = await db.FileStorageSources
            .FirstOrDefaultAsync(s => s.Name == request.Name && s.Id != id);
        
        if (duplicateNameSource != null)
        {
            return BadRequest($"Another file storage source with the name '{request.Name}' already exists.");
        }

        // Verify the host exists
        var hostExists = await db.FileStorageHosts.AnyAsync(h => h.Id == request.FileStorageHostId);
        if (!hostExists)
        {
            return BadRequest("The specified file storage host does not exist.");
        }

        // Update properties
        existingSource.Name = request.Name;
        existingSource.FileStorageHostId = request.FileStorageHostId;
        existingSource.ContainerOrPath = request.ContainerOrPath;
        existingSource.AutoImportFolderName = request.AutoImportFolderName;
        existingSource.IsDefault = request.IsDefault;
        existingSource.IsActive = request.IsActive;
        existingSource.ShouldMoveFiles = request.ShouldMoveFiles;
        existingSource.Description = request.Description;

        await db.SaveChangesAsync();

        // Update categories from request
        if (request.StorageSourceDataTypes != null)
        {
            var desired = request.StorageSourceDataTypes.Distinct().ToList();
            var existing = await db.FileStorageSourceCategories.Where(c => c.FileStorageSourceId == id).ToListAsync();

            var toRemove = existing.Where(c => !desired.Contains(c.DataType)).ToList();
            if (toRemove.Any())
            {
                db.FileStorageSourceCategories.RemoveRange(toRemove);
            }

            foreach (var dt in desired)
            {
                if (!existing.Any(c => c.DataType == dt))
                {
                    db.FileStorageSourceCategories.Add(new FileStorageSourceCategory
                    {
                        Id = Guid.NewGuid(),
                        FileStorageSourceId = id,
                        DataType = dt
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        // Load the updated source with host information for mapping
        var updatedSource = await db.FileStorageSources
            .Include(s => s.FileStorageHost)
            .Include(s => s.Categories)
            .FirstOrDefaultAsync(s => s.Id == id);

        var updatedSourceInfo = _mapper.Map<FileStorageSourceInfo>(updatedSource);
        return Ok(updatedSourceInfo);
    }

    // ---------- ContentReferenceType ↔ FileStorageSource mappings ----------

    /// <summary>
    /// Returns all content reference type → file storage source mappings.
    /// </summary>
    [HttpGet("content-reference-type/mappings")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<ContentReferenceTypeStorageSourceMappingInfo>>> GetAllContentReferenceTypeMappingsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var mappings = await db.ContentReferenceTypeFileStorageSources
            .AsNoTracking()
            .Include(m => m.FileStorageSource)
                .ThenInclude(s => s.FileStorageHost)
            .ToListAsync();

        var result = mappings.Select(m => new ContentReferenceTypeStorageSourceMappingInfo
        {
            Id = m.Id,
            ContentReferenceType = m.ContentReferenceType,
            FileStorageSourceId = m.FileStorageSourceId,
            Priority = m.Priority,
            IsActive = m.IsActive,
            AcceptsUploads = m.AcceptsUploads,
            Source = _mapper.Map<FileStorageSourceInfo>(m.FileStorageSource)
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Returns mappings for a specific ContentReferenceType.
    /// </summary>
    [HttpGet("content-reference-type/{type}/mappings")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    public async Task<ActionResult<List<ContentReferenceTypeStorageSourceMappingInfo>>> GetMappingsForTypeAsync(ContentReferenceType type)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var mappings = await db.ContentReferenceTypeFileStorageSources
            .AsNoTracking()
            .Where(m => m.ContentReferenceType == type)
            .Include(m => m.FileStorageSource)
                .ThenInclude(s => s.FileStorageHost)
            .OrderBy(m => m.Priority)
            .ToListAsync();

        var result = mappings.Select(m => new ContentReferenceTypeStorageSourceMappingInfo
        {
            Id = m.Id,
            ContentReferenceType = m.ContentReferenceType,
            FileStorageSourceId = m.FileStorageSourceId,
            Priority = m.Priority,
            IsActive = m.IsActive,
            AcceptsUploads = m.AcceptsUploads,
            Source = _mapper.Map<FileStorageSourceInfo>(m.FileStorageSource)
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Creates a mapping for type → source.
    /// </summary>
    [HttpPost("content-reference-type/{type}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    public async Task<ActionResult<ContentReferenceTypeStorageSourceMappingInfo>> CreateMappingAsync(ContentReferenceType type, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var source = await db.FileStorageSources.FirstOrDefaultAsync(s => s.Id == sourceId);
        if (source == null) return NotFound("FileStorageSource not found");

        var exists = await db.ContentReferenceTypeFileStorageSources.AnyAsync(m => m.ContentReferenceType == type && m.FileStorageSourceId == sourceId);
        if (exists) return BadRequest("Mapping already exists");

        var mapping = new ContentReferenceTypeFileStorageSource
        {
            Id = Guid.NewGuid(),
            ContentReferenceType = type,
            FileStorageSourceId = sourceId,
            Priority = 0,
            IsActive = true,
            AcceptsUploads = false
        };

        db.ContentReferenceTypeFileStorageSources.Add(mapping);
        await db.SaveChangesAsync();

        var info = new ContentReferenceTypeStorageSourceMappingInfo
        {
            Id = mapping.Id,
            ContentReferenceType = mapping.ContentReferenceType,
            FileStorageSourceId = mapping.FileStorageSourceId,
            Priority = mapping.Priority,
            IsActive = mapping.IsActive,
            AcceptsUploads = mapping.AcceptsUploads,
            Source = _mapper.Map<FileStorageSourceInfo>(source)
        };
        return Ok(info);
    }

    public class UpdateContentReferenceTypeMappingRequest
    {
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public bool AcceptsUploads { get; set; }
    }

    /// <summary>
    /// Updates mapping properties for type → source.
    /// </summary>
    [HttpPut("content-reference-type/{type}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    public async Task<ActionResult<ContentReferenceTypeStorageSourceMappingInfo>> UpdateMappingAsync(ContentReferenceType type, Guid sourceId, [FromBody] UpdateContentReferenceTypeMappingRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var mapping = await db.ContentReferenceTypeFileStorageSources
            .Include(m => m.FileStorageSource)
                .ThenInclude(s => s.FileStorageHost)
            .FirstOrDefaultAsync(m => m.ContentReferenceType == type && m.FileStorageSourceId == sourceId);
        if (mapping == null) return NotFound();

        mapping.Priority = request.Priority;
        mapping.IsActive = request.IsActive;
        mapping.AcceptsUploads = request.AcceptsUploads;
        await db.SaveChangesAsync();

        var info = new ContentReferenceTypeStorageSourceMappingInfo
        {
            Id = mapping.Id,
            ContentReferenceType = mapping.ContentReferenceType,
            FileStorageSourceId = mapping.FileStorageSourceId,
            Priority = mapping.Priority,
            IsActive = mapping.IsActive,
            AcceptsUploads = mapping.AcceptsUploads,
            Source = _mapper.Map<FileStorageSourceInfo>(mapping.FileStorageSource)
        };
        return Ok(info);
    }

    /// <summary>
    /// Deletes mapping for type → source.
    /// </summary>
    [HttpDelete("content-reference-type/{type}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    public async Task<IActionResult> DeleteMappingAsync(ContentReferenceType type, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var mapping = await db.ContentReferenceTypeFileStorageSources.FirstOrDefaultAsync(m => m.ContentReferenceType == type && m.FileStorageSourceId == sourceId);
        if (mapping == null) return NotFound();
        db.ContentReferenceTypeFileStorageSources.Remove(mapping);
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Deletes a file storage source.
    /// </summary>
    /// <param name="id">The ID of the file storage source to delete.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id}")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFileStorageSourceAsync(Guid id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var source = await db.FileStorageSources.FirstOrDefaultAsync(s => s.Id == id);
        if (source == null)
        {
            return NotFound();
        }

        // Check if the source is in use by any document processes or libraries
        var processAssociations = await db.DocumentProcessFileStorageSources
            .CountAsync(dps => dps.FileStorageSourceId == id);
        
        var libraryAssociations = await db.DocumentLibraryFileStorageSources
            .CountAsync(dls => dls.FileStorageSourceId == id);

        if (processAssociations > 0 || libraryAssociations > 0)
        {
            return BadRequest("Cannot delete file storage source because it is currently in use by document processes or libraries.");
        }

        db.FileStorageSources.Remove(source);
        await db.SaveChangesAsync();

        return NoContent();
    }

    #region Legacy method overloads for backward compatibility

    /// <summary>
    /// Creates a new file storage source (legacy overload).
    /// </summary>
    /// <param name="sourceInfo">The file storage source information to create.</param>
    /// <returns>The created file storage source information.</returns>
    [HttpPost("legacy")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Obsolete("Use POST /api/file-storage-sources with CreateFileStorageSourceRequest instead")]
    public async Task<ActionResult<FileStorageSourceInfo>> CreateFileStorageSourceLegacyAsync([FromBody] FileStorageSourceInfo sourceInfo)
    {
        var request = new CreateFileStorageSourceRequest
        {
            Name = sourceInfo.Name,
            FileStorageHostId = sourceInfo.FileStorageHostId,
            ContainerOrPath = sourceInfo.ContainerOrPath,
            AutoImportFolderName = sourceInfo.AutoImportFolderName,
            IsDefault = sourceInfo.IsDefault,
            IsActive = sourceInfo.IsActive,
            ShouldMoveFiles = sourceInfo.ShouldMoveFiles,
            Description = sourceInfo.Description,
            StorageSourceDataTypes = sourceInfo.StorageSourceDataTypes
        };

        return await CreateFileStorageSourceAsync(request);
    }

    /// <summary>
    /// Updates an existing file storage source (legacy overload).
    /// </summary>
    /// <param name="id">The ID of the file storage source to update.</param>
    /// <param name="sourceInfo">The updated file storage source information.</param>
    /// <returns>The updated file storage source information.</returns>
    [HttpPut("{id}/legacy")]
    [RequiresPermission(PermissionKeys.AlterSystemConfiguration)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Obsolete("Use PUT /api/file-storage-sources/{id} with UpdateFileStorageSourceRequest instead")]
    public async Task<ActionResult<FileStorageSourceInfo>> UpdateFileStorageSourceLegacyAsync(Guid id, [FromBody] FileStorageSourceInfo sourceInfo)
    {
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
            Description = sourceInfo.Description,
            StorageSourceDataTypes = sourceInfo.StorageSourceDataTypes
        };

        return await UpdateFileStorageSourceAsync(id, request);
    }

    #endregion

    /// <summary>
    /// Associates a file storage source with a document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>No content if successful.</returns>
    [HttpPost("document-process/{processId}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateSourceWithProcessAsync(Guid processId, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Verify both entities exist
        var processExists = await db.DynamicDocumentProcessDefinitions.AnyAsync(p => p.Id == processId);
        var sourceExists = await db.FileStorageSources.AnyAsync(s => s.Id == sourceId);

        if (!processExists)
        {
            return NotFound("Document process not found.");
        }

        if (!sourceExists)
        {
            return NotFound("File storage source not found.");
        }

        // Check if association already exists
        var existingAssociation = await db.DocumentProcessFileStorageSources
            .FirstOrDefaultAsync(dps => dps.DocumentProcessId == processId && dps.FileStorageSourceId == sourceId);

        if (existingAssociation != null)
        {
            return BadRequest("Association already exists.");
        }

        var association = new DocumentProcessFileStorageSource
        {
            DocumentProcessId = processId,
            FileStorageSourceId = sourceId
        };

        db.DocumentProcessFileStorageSources.Add(association);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Disassociates a file storage source from a document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("document-process/{processId}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisassociateSourceFromProcessAsync(Guid processId, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var association = await db.DocumentProcessFileStorageSources
            .FirstOrDefaultAsync(dps => dps.DocumentProcessId == processId && dps.FileStorageSourceId == sourceId);

        if (association == null)
        {
            return NotFound("Association not found.");
        }

        // Check if this is the last source for the process
        var sourceCount = await db.DocumentProcessFileStorageSources
            .CountAsync(dps => dps.DocumentProcessId == processId);

        if (sourceCount <= 1)
        {
            return BadRequest("Cannot remove the last file storage source from a document process. At least one source is required.");
        }

        // Before removing the association, clean up orphaned IngestedDocuments
        await CleanupOrphanedIngestedDocumentsForProcessAsync(db, processId, sourceId);

        db.DocumentProcessFileStorageSources.Remove(association);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Associates a file storage source with a document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>No content if successful.</returns>
    [HttpPost("document-library/{libraryId}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssociateSourceWithLibraryAsync(Guid libraryId, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Verify both entities exist
        var libraryExists = await db.DocumentLibraries.AnyAsync(l => l.Id == libraryId);
        var sourceExists = await db.FileStorageSources.AnyAsync(s => s.Id == sourceId);

        if (!libraryExists)
        {
            return NotFound("Document library not found.");
        }

        if (!sourceExists)
        {
            return NotFound("File storage source not found.");
        }

        // Check if association already exists
        var existingAssociation = await db.DocumentLibraryFileStorageSources
            .FirstOrDefaultAsync(dls => dls.DocumentLibraryId == libraryId && dls.FileStorageSourceId == sourceId);

        if (existingAssociation != null)
        {
            return BadRequest("Association already exists.");
        }

        var association = new DocumentLibraryFileStorageSource
        {
            DocumentLibraryId = libraryId,
            FileStorageSourceId = sourceId
        };

        db.DocumentLibraryFileStorageSources.Add(association);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Disassociates a file storage source from a document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("document-library/{libraryId}/sources/{sourceId}")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisassociateSourceFromLibraryAsync(Guid libraryId, Guid sourceId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var association = await db.DocumentLibraryFileStorageSources
            .FirstOrDefaultAsync(dls => dls.DocumentLibraryId == libraryId && dls.FileStorageSourceId == sourceId);

        if (association == null)
        {
            return NotFound("Association not found.");
        }

        // Check if this is the last source for the library
        var sourceCount = await db.DocumentLibraryFileStorageSources
            .CountAsync(dls => dls.DocumentLibraryId == libraryId);

        if (sourceCount <= 1)
        {
            return BadRequest("Cannot remove the last file storage source from a document library. At least one source is required.");
        }

        // Before removing the association, clean up orphaned IngestedDocuments
        await CleanupOrphanedIngestedDocumentsForLibraryAsync(db, libraryId, sourceId);

        db.DocumentLibraryFileStorageSources.Remove(association);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Gets file storage source associations for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>A list of file storage source associations for the process.</returns>
    [HttpGet("document-process/{processId}/associations")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<DocumentProcessFileStorageSourceInfo>>> GetFileStorageSourceAssociationsByProcessIdAsync(Guid processId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var associations = await db.DocumentProcessFileStorageSources
            .AsNoTracking()
            .Where(dps => dps.DocumentProcessId == processId)
            .Include(dps => dps.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dps => dps.DocumentProcess)
            .OrderBy(dps => dps.Priority)
            .ThenBy(dps => dps.FileStorageSource.Name)
            .ToListAsync();

        var associationInfos = _mapper.Map<List<DocumentProcessFileStorageSourceInfo>>(associations);
        return Ok(associationInfos);
    }

    /// <summary>
    /// Gets file storage source associations for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>A list of file storage source associations for the library.</returns>
    [HttpGet("document-library/{libraryId}/associations")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<List<DocumentLibraryFileStorageSourceInfo>>> GetFileStorageSourceAssociationsByLibraryIdAsync(Guid libraryId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var associations = await db.DocumentLibraryFileStorageSources
            .AsNoTracking()
            .Where(dls => dls.DocumentLibraryId == libraryId)
            .Include(dls => dls.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dls => dls.DocumentLibrary)
            .OrderBy(dls => dls.Priority)
            .ThenBy(dls => dls.FileStorageSource.Name)
            .ToListAsync();

        var associationInfos = _mapper.Map<List<DocumentLibraryFileStorageSourceInfo>>(associations);
        return Ok(associationInfos);
    }

    /// <summary>
    /// Updates the upload acceptance status and other properties for a document process file storage source association.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated association information.</returns>
    [HttpPut("document-process/{processId}/sources/{sourceId}/association")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<DocumentProcessFileStorageSourceInfo>> UpdateProcessSourceAssociationAsync(Guid processId, Guid sourceId, [FromBody] UpdateProcessSourceAssociationRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var association = await db.DocumentProcessFileStorageSources
            .Include(dps => dps.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dps => dps.DocumentProcess)
            .FirstOrDefaultAsync(dps => dps.DocumentProcessId == processId && dps.FileStorageSourceId == sourceId);

        if (association == null)
        {
            return NotFound("Association not found.");
        }

        // If setting AcceptsUploads to true, ensure no other association for this process accepts uploads
        if (request.AcceptsUploads && !association.AcceptsUploads)
        {
            var existingUploadAssociation = await db.DocumentProcessFileStorageSources
                .FirstOrDefaultAsync(dps => dps.DocumentProcessId == processId && dps.AcceptsUploads && dps.FileStorageSourceId != sourceId);

            if (existingUploadAssociation != null)
            {
                existingUploadAssociation.AcceptsUploads = false;
                db.DocumentProcessFileStorageSources.Update(existingUploadAssociation);
            }
        }

        // Update the association properties
        association.AcceptsUploads = request.AcceptsUploads;
        association.Priority = request.Priority;
        association.IsActive = request.IsActive;

        db.DocumentProcessFileStorageSources.Update(association);
        await db.SaveChangesAsync();

        var updatedAssociationInfo = _mapper.Map<DocumentProcessFileStorageSourceInfo>(association);
        return Ok(updatedAssociationInfo);
    }

    /// <summary>
    /// Updates the upload acceptance status and other properties for a document library file storage source association.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated association information.</returns>
    [HttpPut("document-library/{libraryId}/sources/{sourceId}/association")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    public async Task<ActionResult<DocumentLibraryFileStorageSourceInfo>> UpdateLibrarySourceAssociationAsync(Guid libraryId, Guid sourceId, [FromBody] UpdateLibrarySourceAssociationRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var association = await db.DocumentLibraryFileStorageSources
            .Include(dls => dls.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dls => dls.DocumentLibrary)
            .FirstOrDefaultAsync(dls => dls.DocumentLibraryId == libraryId && dls.FileStorageSourceId == sourceId);

        if (association == null)
        {
            return NotFound("Association not found.");
        }

        // If setting AcceptsUploads to true, ensure no other association for this library accepts uploads
        if (request.AcceptsUploads && !association.AcceptsUploads)
        {
            var existingUploadAssociation = await db.DocumentLibraryFileStorageSources
                .FirstOrDefaultAsync(dls => dls.DocumentLibraryId == libraryId && dls.AcceptsUploads && dls.FileStorageSourceId != sourceId);

            if (existingUploadAssociation != null)
            {
                existingUploadAssociation.AcceptsUploads = false;
                db.DocumentLibraryFileStorageSources.Update(existingUploadAssociation);
            }
        }

        // Update the association properties
        association.AcceptsUploads = request.AcceptsUploads;
        association.Priority = request.Priority;
        association.IsActive = request.IsActive;

        db.DocumentLibraryFileStorageSources.Update(association);
        await db.SaveChangesAsync();

        var updatedAssociationInfo = _mapper.Map<DocumentLibraryFileStorageSourceInfo>(association);
        return Ok(updatedAssociationInfo);
    }

    /// <summary>
    /// Gets the file storage source that accepts uploads for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>The file storage source that accepts uploads, or null if none is configured.</returns>
    [HttpGet("document-library/{libraryId}/upload-source")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<DocumentLibraryFileStorageSourceInfo?>> GetUploadSourceForLibraryAsync(Guid libraryId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var uploadAssociation = await db.DocumentLibraryFileStorageSources
            .AsNoTracking()
            .Where(dls => dls.DocumentLibraryId == libraryId && dls.AcceptsUploads && dls.IsActive)
            .Include(dls => dls.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dls => dls.DocumentLibrary)
            .FirstOrDefaultAsync();

        if (uploadAssociation == null)
        {
            return NotFound("No upload-enabled file storage source found for this document library.");
        }

        var associationInfo = _mapper.Map<DocumentLibraryFileStorageSourceInfo>(uploadAssociation);
        return Ok(associationInfo);
    }

    /// <summary>
    /// Gets the file storage source that accepts uploads for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>The file storage source that accepts uploads, or null if none is configured.</returns>
    [HttpGet("document-process/{processId}/upload-source")]
    [RequiresPermission(PermissionKeys.AlterDocumentProcessesAndLibraries)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult<DocumentProcessFileStorageSourceInfo?>> GetUploadSourceForProcessAsync(Guid processId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var uploadAssociation = await db.DocumentProcessFileStorageSources
            .AsNoTracking()
            .Where(dps => dps.DocumentProcessId == processId && dps.AcceptsUploads && dps.IsActive)
            .Include(dps => dps.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .Include(dps => dps.DocumentProcess)
            .FirstOrDefaultAsync();

        if (uploadAssociation == null)
        {
            return NotFound("No upload-enabled file storage source found for this document process.");
        }

        var associationInfo = _mapper.Map<DocumentProcessFileStorageSourceInfo>(uploadAssociation);
        return Ok(associationInfo);
    }

    /// <summary>
    /// Cleans up orphaned IngestedDocuments and their FileAcknowledgment relationships when removing a process association.
    /// </summary>
    private async Task CleanupOrphanedIngestedDocumentsForProcessAsync(DocGenerationDbContext db, Guid processId, Guid sourceId)
    {
        // Get the document process and file storage source details
        var documentProcess = await db.DynamicDocumentProcessDefinitions
            .FirstOrDefaultAsync(dp => dp.Id == processId);

        var fileStorageSource = await db.FileStorageSources
            .FirstOrDefaultAsync(fss => fss.Id == sourceId);

        if (documentProcess == null || fileStorageSource == null)
        {
            _logger.LogWarning("Could not find DocumentProcess {ProcessId} or FileStorageSource {SourceId} for cleanup", 
                processId, sourceId);
            return;
        }

        // Find IngestedDocuments that match this DP + Container combination
        var containerName = $"FileStorageSource:{sourceId}";
        var orphanedDocuments = await db.IngestedDocuments
            .Where(doc => doc.DocumentLibraryType == Microsoft.Greenlight.Shared.Enums.DocumentLibraryType.PrimaryDocumentProcessLibrary &&
                         doc.DocumentLibraryOrProcessName == documentProcess.ShortName &&
                         doc.Container == containerName)
            .ToListAsync();

        if (!orphanedDocuments.Any())
        {
            _logger.LogInformation("No orphaned IngestedDocuments found for DocumentProcess {ProcessName} and FileStorageSource {SourceId}", 
                documentProcess.ShortName, sourceId);
            return;
        }

        _logger.LogInformation("Found {Count} orphaned IngestedDocuments for DocumentProcess {ProcessName} and FileStorageSource {SourceId}", 
            orphanedDocuments.Count, documentProcess.ShortName, sourceId);

        // Remove IngestedDocumentFileAcknowledgment relationships first
        var documentIds = orphanedDocuments.Select(d => d.Id).ToList();
        var acknowledgmentRelationships = await db.IngestedDocumentFileAcknowledgments
            .Where(idfa => documentIds.Contains(idfa.IngestedDocumentId))
            .ToListAsync();

        if (acknowledgmentRelationships.Any())
        {
            db.IngestedDocumentFileAcknowledgments.RemoveRange(acknowledgmentRelationships);
            _logger.LogInformation("Removing {Count} IngestedDocumentFileAcknowledgment relationships", 
                acknowledgmentRelationships.Count);
        }

        // Remove the orphaned IngestedDocuments
        db.IngestedDocuments.RemoveRange(orphanedDocuments);
        _logger.LogInformation("Removing {Count} orphaned IngestedDocuments", orphanedDocuments.Count);
    }

    /// <summary>
    /// Cleans up orphaned IngestedDocuments and their FileAcknowledgment relationships when removing a library association.
    /// </summary>
    private async Task CleanupOrphanedIngestedDocumentsForLibraryAsync(DocGenerationDbContext db, Guid libraryId, Guid sourceId)
    {
        // Get the document library and file storage source details
        var documentLibrary = await db.DocumentLibraries
            .FirstOrDefaultAsync(dl => dl.Id == libraryId);

        var fileStorageSource = await db.FileStorageSources
            .FirstOrDefaultAsync(fss => fss.Id == sourceId);

        if (documentLibrary == null || fileStorageSource == null)
        {
            _logger.LogWarning("Could not find DocumentLibrary {LibraryId} or FileStorageSource {SourceId} for cleanup", 
                libraryId, sourceId);
            return;
        }

        // Find IngestedDocuments that match this DL + Container combination
        var containerName = $"FileStorageSource:{sourceId}";
        var orphanedDocuments = await db.IngestedDocuments
            .Where(doc => doc.DocumentLibraryType == Microsoft.Greenlight.Shared.Enums.DocumentLibraryType.AdditionalDocumentLibrary &&
                         doc.DocumentLibraryOrProcessName == documentLibrary.ShortName &&
                         doc.Container == containerName)
            .ToListAsync();

        if (!orphanedDocuments.Any())
        {
            _logger.LogInformation("No orphaned IngestedDocuments found for DocumentLibrary {LibraryName} and FileStorageSource {SourceId}", 
                documentLibrary.ShortName, sourceId);
            return;
        }

        _logger.LogInformation("Found {Count} orphaned IngestedDocuments for DocumentLibrary {LibraryName} and FileStorageSource {SourceId}", 
            orphanedDocuments.Count, documentLibrary.ShortName, sourceId);

        // Remove IngestedDocumentFileAcknowledgment relationships first
        var documentIds = orphanedDocuments.Select(d => d.Id).ToList();
        var acknowledgmentRelationships = await db.IngestedDocumentFileAcknowledgments
            .Where(idfa => documentIds.Contains(idfa.IngestedDocumentId))
            .ToListAsync();

        if (acknowledgmentRelationships.Any())
        {
            db.IngestedDocumentFileAcknowledgments.RemoveRange(acknowledgmentRelationships);
            _logger.LogInformation("Removing {Count} IngestedDocumentFileAcknowledgment relationships", 
                acknowledgmentRelationships.Count);
        }

        // Remove the orphaned IngestedDocuments
        db.IngestedDocuments.RemoveRange(orphanedDocuments);
        _logger.LogInformation("Removing {Count} orphaned IngestedDocuments", orphanedDocuments.Count);
    }
}
