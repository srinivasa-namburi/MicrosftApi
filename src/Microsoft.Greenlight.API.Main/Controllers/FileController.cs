using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Microsoft.KernelMemory.Pipeline;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Models.Authorization;
using Microsoft.Greenlight.Shared.Services.ContentReference;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Controller for handling file operations such as upload and download.
/// </summary>
[Route("/api/file")]
public class FileController : BaseController
{
    private readonly AzureFileHelper _fileHelper;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDocumentProcessInfoService _documentLibraryProcessService;
    private readonly IFileStorageServiceFactory _fileStorageServiceFactory;
    private readonly ILogger<FileController> _logger;
    private readonly IContentReferenceService _contentReferenceService;
    private readonly IFileUrlResolverService _fileUrlResolverService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileController"/> class.
    /// </summary>
    /// <param name="fileHelper">The file helper for Azure operations.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="documentLibraryProcessService">The document library process service.</param>
    /// <param name="fileStorageServiceFactory">The file storage service factory.</param>
    /// <param name="logger"></param>
    public FileController(
        AzureFileHelper fileHelper,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IDocumentProcessInfoService documentLibraryProcessService,
        IFileStorageServiceFactory fileStorageServiceFactory,
        ILogger<FileController> logger,
        IContentReferenceService contentReferenceService,
        IFileUrlResolverService fileUrlResolverService)
    {
        _fileHelper = fileHelper;
        _dbContext = dbContext;
        _mapper = mapper;
        _documentLibraryProcessService = documentLibraryProcessService;
        _fileStorageServiceFactory = fileStorageServiceFactory;
        _logger = logger;
        _contentReferenceService = contentReferenceService;
        _fileUrlResolverService = fileUrlResolverService;
    }

    /// <summary>
    /// Downloads a file from the specified URL.
    /// </summary>
    /// <param name="fileUrl">The URL of the file to download.</param>
    /// <param name="disposition">Optional content-disposition behavior: 'inline' or 'attachment'. Defaults to current behavior (attachment with filename).</param>
    /// <param name="contentTypeOverride">Optional explicit content-type override (e.g., application/pdf). If not supplied, detected from file name.</param>
    /// <param name="page">Optional page hint for clients/viewers (ignored by server). Present for compatibility with deep-linking like '#page=n'.</param>
    /// <returns>The file stream if found
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file can't be found using the file url provided
    /// </returns>
    [HttpGet("download/{fileUrl}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadFile(string fileUrl, [FromQuery] string? disposition = null, [FromQuery] string? contentTypeOverride = null, [FromQuery] int? page = null)
    {
        var decodedFileUrl = Uri.UnescapeDataString(fileUrl);

        var stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(decodedFileUrl);
        if (stream == null)
        {
            return NotFound();
        }

        var contentType = string.IsNullOrWhiteSpace(contentTypeOverride)
            ? new MimeTypesDetection().GetFileType(decodedFileUrl)
            : contentTypeOverride;

        // Try to get DisplayFileName from FileAcknowledgmentRecord first
        var fileName = await GetDisplayFileNameAsync(decodedFileUrl)
                       ?? decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];

        // If inline requested, return FileStreamResult without forcing download header
        if (!string.IsNullOrWhiteSpace(disposition) && disposition.Equals("inline", StringComparison.OrdinalIgnoreCase))
        {
            // The optional 'page' query is intentionally ignored by the server; viewers can use URL fragments.
            return File(stream, contentType);
        }

        // Default behavior (download/attachment with filename)
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Downloads a file by its link ID.
    /// </summary>
    /// <param name="linkId">The link ID of the file to download.</param>
    /// <param name="disposition">Optional content-disposition behavior: 'inline' or 'attachment'. Defaults to current behavior (attachment with filename).</param>
    /// <param name="contentTypeOverride">Optional explicit content-type override (e.g., application/pdf). If not supplied, detected from file name.</param>
    /// <param name="page">Optional page hint for clients/viewers (ignored by server). Present for compatibility with deep-linking like '#page=n'.</param>
    /// <returns>The file stream if found
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file can't be found using the file url provided
    /// </returns>
    [HttpGet("download/asset/{linkId}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadFileById(string linkId, [FromQuery] string? disposition = null, [FromQuery] string? contentTypeOverride = null, [FromQuery] int? page = null)
    {
        var file = _dbContext.ExportedDocumentLinks.FirstOrDefault(edl => edl.Id == new Guid(linkId));
        if (file == null)
        {
            return NotFound();
        }

        var decodedFileUrl = Uri.UnescapeDataString(file.AbsoluteUrl);

        var stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(decodedFileUrl);
        if (stream == null)
        {
            return NotFound();
        }

        var contentType = string.IsNullOrWhiteSpace(contentTypeOverride)
            ? new MimeTypesDetection().GetFileType(decodedFileUrl)
            : contentTypeOverride;

        // Try to get DisplayFileName from FileAcknowledgmentRecord first
        var fileName = await GetDisplayFileNameAsync(decodedFileUrl)
                       ?? decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];

        if (!string.IsNullOrWhiteSpace(disposition) && disposition.Equals("inline", StringComparison.OrdinalIgnoreCase))
        {
            // The optional 'page' query is intentionally ignored by the server; viewers can use URL fragments.
            return File(stream, contentType);
        }

        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Downloads a file by its external link asset ID.
    /// </summary>
    /// <param name="assetId">The external link asset ID of the file to download.</param>
    /// <param name="disposition">Optional content-disposition behavior: 'inline' or 'attachment'. Defaults to current behavior (attachment with filename).</param>
    /// <param name="contentTypeOverride">Optional explicit content-type override (e.g., application/pdf). If not supplied, detected from file name.</param>
    /// <param name="page">Optional page hint for clients/viewers (ignored by server). Present for compatibility with deep-linking like '#page=n'.</param>
    /// <returns>The file stream if found
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file can't be found using the asset ID provided
    /// </returns>
    [HttpGet("download/external-asset/{assetId}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadExternalLinkAsset(string assetId, [FromQuery] string? disposition = null, [FromQuery] string? contentTypeOverride = null, [FromQuery] int? page = null)
    {
        var asset = await _dbContext.ExternalLinkAssets
            .Include(ela => ela.FileStorageSource)
            .ThenInclude(fss => fss!.FileStorageHost)
            .AsNoTracking()
            .FirstOrDefaultAsync(ela => ela.Id == new Guid(assetId));

        if (asset == null)
        {
            return NotFound();
        }

        Stream? stream = null;

        try
        {
            if (asset.FileStorageSourceId.HasValue && asset.FileStorageSource != null)
            {
                // Use the proper FileStorageService for this source
                var sourceInfo = _mapper.Map<FileStorageSourceInfo>(asset.FileStorageSource);
                var fileStorageService = _fileStorageServiceFactory.CreateService(sourceInfo);

                // For FileStorageService, use the asset's filename as the relative path
                // The URL is just for display/reference - the actual file is stored by filename
                string relativePath = asset.FileName;

                _logger.LogDebug("Downloading external asset {AssetId} using FileStorageService {ProviderType} with relative path {RelativePath}",
                    assetId, fileStorageService.ProviderType, relativePath);

                stream = await fileStorageService.GetFileStreamAsync(relativePath);
            }
            else
            {
                // Fallback to legacy approach for assets created before FileStorageSourceId was added
                _logger.LogDebug("Using legacy download approach for external asset {AssetId} (no FileStorageSourceId)", assetId);

                if (Uri.IsWellFormedUriString(asset.Url, UriKind.Absolute))
                {
                    // This is a full URL (likely blob storage)
                    stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(asset.Url);
                }
                else
                {
                    // This is likely a local file path
                    if (System.IO.File.Exists(asset.Url))
                    {
                        stream = new FileStream(asset.Url, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                    }
                }
            }

            if (stream == null)
            {
                return NotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(contentTypeOverride)
                ? (asset.MimeType ?? new MimeTypesDetection().GetFileType(asset.FileName))
                : contentTypeOverride;

            if (!string.IsNullOrWhiteSpace(disposition) && disposition.Equals("inline", StringComparison.OrdinalIgnoreCase))
            {
                // The optional 'page' query is intentionally ignored by the server; viewers can use URL fragments.
                return File(stream, contentType);
            }

            return File(stream, contentType, asset.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading external link asset {AssetId} from URL {Url}", assetId, asset.Url);
            return NotFound();
        }
    }

    /// <summary>
    /// Resolves a download URL for a file acknowledgment record.
    /// Uses the FileUrlResolverService to create or retrieve an ExternalLinkAsset URL.
    /// </summary>
    /// <param name="acknowledgmentId">The ID of the file acknowledgment record.</param>
    /// <returns>The resolved URL for downloading the file.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully with the resolved URL
    ///     404 Not Found: When the acknowledgment record is not found
    /// </returns>
    [HttpGet("resolve-url/acknowledgment/{acknowledgmentId}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<IActionResult> ResolveFileAcknowledgmentUrl(Guid acknowledgmentId)
    {
        try
        {
            var acknowledgment = await _dbContext.FileAcknowledgmentRecords
                .Include(f => f.FileStorageSource)
                .FirstOrDefaultAsync(f => f.Id == acknowledgmentId);

            if (acknowledgment == null)
            {
                return NotFound($"File acknowledgment record {acknowledgmentId} not found");
            }

            var resolvedUrl = await _fileUrlResolverService.ResolveUrlAsync(acknowledgment);
            
            return Ok(new { url = resolvedUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL for file acknowledgment {AcknowledgmentId}", acknowledgmentId);
            return BadRequest($"Failed to resolve URL: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads a file to the specified container with a random file name.
    /// </summary>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The access URL of the uploaded file.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When there is no file provided to upload, 
    ///         the Container Name provided is invalid, 
    ///         or the file name is missing
    /// </returns>
    [HttpPost("upload/{containerName}/{fileName}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<string>]
    [SwaggerIgnore]
    public async Task<IActionResult> UploadFile(string containerName, string fileName, [FromForm] IFormFile? file = null)
    {
        // URL Decode the file name and container name
        fileName = Uri.UnescapeDataString(fileName);
        containerName = Uri.UnescapeDataString(containerName);

        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate container name
        if (!await IsValidContainerNameAsync(containerName))
        {
            return BadRequest("Invalid container name. Must be a valid container.");
        }

        // Validate file name
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        // Read the file stream
        using var stream = file.OpenReadStream();
        // Generate a random file name for the backend in blob storage
        var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);
        // Upload the file to blob storage
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);
        // Save the file information in the database
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);
        // Get the access URL for the file
        var fileAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(exportedDocumentLink.Id);

        return Ok(fileAccessUrl);
    }

    /// <summary>
    /// Uploads a file to the specified container with the provided file name.
    /// </summary>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The access URL of the uploaded file.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When there is no file provided to upload, 
    ///         the Container Name provided is invalid, 
    ///         or the file name is missing
    /// </returns>
    [HttpPost("upload/direct/{containerName}/{fileName}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<string>]
    [SwaggerIgnore]
    public async Task<IActionResult> UploadFileDirect(string containerName, string fileName, [FromForm] IFormFile? file = null)
    {
        // URL Decode the file name and container name
        fileName = Uri.UnescapeDataString(fileName);
        containerName = Uri.UnescapeDataString(containerName);

        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate container name
        if (!await IsValidContainerNameAsync(containerName))
        {
            return BadRequest("Invalid container name. Must be a valid container.");
        }

        // Validate file name
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        // Read the file stream
        using var stream = file.OpenReadStream();
        // Use the provided file name as the blob file name
        var blobFileName = fileName;
        // Upload the file to blob storage
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);
        // Get the access URL for the file
        var fileAccessUrl = _fileHelper.GetProxiedBlobUrl(blobUrl);

        return Ok(fileAccessUrl);
    }

    /// <summary>
    /// Uploads a file as a temporary reference that can be used in content references.
    /// Supports deduplication based on file hash to avoid storing duplicate files.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>A ContentReferenceItemInfo representing the uploaded file.
    /// Produces Status Codes:
    ///     200 OK: When completed successfully
    ///     400 Bad Request: When there is no file provided to upload or the file name is missing
    /// </returns>
    [HttpPost("upload/reference/{fileName}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<ContentReferenceItemInfo>]
    [SwaggerIgnore]
    public async Task<ActionResult<ContentReferenceItemInfo>> UploadTemporaryReferenceFile(string fileName, [FromForm] IFormFile? file = null)
    {
        // Validate input
        fileName = Uri.UnescapeDataString(fileName);
        var validationResult = ValidateFileUpload(file, fileName);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Prefer FileStorageSource-based upload (ExternalLinkAsset). Fallback to legacy if not configured.
        try
        {
            // Prefer mapping via ContentReferenceTypeFileStorageSource for ExternalLinkAsset
            var mappingSource = await _dbContext.ContentReferenceTypeFileStorageSources
                .Include(m => m.FileStorageSource)
                    .ThenInclude(s => s.FileStorageHost)
                .AsNoTracking()
                .Where(m => m.IsActive && m.AcceptsUploads && m.ContentReferenceType == ContentReferenceType.ExternalLinkAsset)
                .OrderBy(m => m.Priority)
                .Select(m => m.FileStorageSource)
                .FirstOrDefaultAsync(); // This should in reality only ever return one because AcceptsUploads can only be true on 0 or 1 file storage sources per ContentReferenceType.

            // Prefer a ContentReference storage source on the default host (ConnectionString == "default") if mapping not found
            var contentRefFileStorageSource = mappingSource ?? await _dbContext.FileStorageSources
                .Include(s => s.FileStorageHost)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IsActive &&
                                          s.StorageSourceDataType == FileStorageSourceDataType.ContentReference &&
                                          s.FileStorageHost != null && s.FileStorageHost.ConnectionString == "default");

            var selectedSource = contentRefFileStorageSource;
            if (selectedSource == null)
            {
                // Fallback to any default active source
                selectedSource = await _dbContext.FileStorageSources
                    .Include(s => s.FileStorageHost)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.IsDefault && s.IsActive);
            }

            if (selectedSource != null)
            {
                var sourceInfo = _mapper.Map<FileStorageSourceInfo>(selectedSource);
                var fileStorageService = _fileStorageServiceFactory.CreateService(sourceInfo);

                // Upload file to the storage service (for ContentReference, upload to container root)
                await using var stream = file!.OpenReadStream();
                string? folderPath = null; // upload directly to container
                var relativePath = await fileStorageService.UploadFileAsync(fileName, stream, folderPath);

                // Persist metadata and create ExternalLinkAsset record
                var uploadResult = await fileStorageService.SaveFileInfoAsync(relativePath, fileName);

                // Use ContentReferenceService to create the reference (handles deduplication, FAKs, RAG text, etc.)
                var contentReferenceItemInfo = await _contentReferenceService.CreateExternalLinkAssetReferenceAsync(
                    uploadResult.ExternalLinkAssetId,
                    fileName,
                    uploadResult.FileHash);

                return Ok(contentReferenceItemInfo);
            }
        }
        catch (Exception ex)
        {
            // Log and fall through to legacy path
            _logger.LogWarning(ex, "FileStorageSource-based temporary upload failed. Falling back to legacy path for {FileName}", fileName);
        }

        // Legacy ExportedDocumentLink path (ExternalFile) as fallback
        const string containerName = "temporary-references";
        string blobFileName = string.Empty;

        try
        {
            var (blobUrl, exportedDocumentLink) = await UploadFileAndSaveInfoAsync(file!, fileName, containerName);
            blobFileName = Path.GetFileName(new Uri(blobUrl).LocalPath);

            // Use ContentReferenceService to create the reference (handles deduplication)
            var contentReferenceInfo = await _contentReferenceService.CreateExternalFileReferenceAsync(
                exportedDocumentLink, fileName);

            // If a duplicate was found, clean up the uploaded file
            if (!string.IsNullOrEmpty(exportedDocumentLink.FileHash))
            {
                var duplicate = await _contentReferenceService.FindDuplicateByFileHashAsync(
                    exportedDocumentLink.FileHash, ContentReferenceType.ExternalFile);
                if (duplicate != null && duplicate.ContentReferenceSourceId != exportedDocumentLink.Id)
                {
                    await CleanupDuplicateUploadAsync(containerName, blobFileName, blobUrl, exportedDocumentLink);
                }
            }

            return Ok(contentReferenceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file (legacy path) {FileName}", fileName);
            await CleanupFailedUploadAsync(containerName, blobFileName);
            var fallbackReferenceInfo = await _contentReferenceService.CreateFallbackReferenceAsync(
                fileName, ContentReferenceType.ExternalFile, ex.Message);
            return Ok(fallbackReferenceInfo);
        }
    }

    /// <summary>
    /// Validates file upload parameters.
    /// </summary>
    private ActionResult? ValidateFileUpload(IFormFile? file, string fileName)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        // Add file size validation (e.g., 100MB limit)
        const long maxFileSize = 100 * 1024 * 1024; // 100MB
        if (file.Length > maxFileSize)
        {
            return BadRequest($"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB.");
        }

        return null;
    }

    /// <summary>
    /// Uploads a file to blob storage and saves its information to the database.
    /// </summary>
    private async Task<(string blobUrl, ExportedDocumentLink exportedDocumentLink)> UploadFileAndSaveInfoAsync(
        IFormFile file, string fileName, string containerName)
    {
        await using var stream = file.OpenReadStream();

        // Generate a unique file name for blob storage
        var blobFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        // Upload to blob storage
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);

        // Save file information to database
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);

        return (blobUrl, exportedDocumentLink);
    }


    /// <summary>
    /// Cleans up a duplicate file upload.
    /// </summary>
    private async Task CleanupDuplicateUploadAsync(
        string containerName, string blobFileName, string blobUrl, ExportedDocumentLink exportedDocumentLink)
    {
        try
        {
            // Delete the uploaded blob
            await _fileHelper.DeleteBlobAsync(containerName, blobFileName);

            // Remove the document link from database
            _dbContext.ExportedDocumentLinks.Remove(exportedDocumentLink);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Cleaned up duplicate upload: {BlobUrl}", blobUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up duplicate file upload - continuing anyway");
        }
    }


    /// <summary>
    /// Cleans up resources after a failed upload.
    /// </summary>
    private async Task CleanupFailedUploadAsync(string containerName, string blobFileName)
    {
        if (string.IsNullOrEmpty(blobFileName))
        {
            return;
        }

        try
        {
            await _fileHelper.DeleteBlobAsync(containerName, blobFileName);
            _logger.LogInformation("Cleaned up failed upload: {BlobFileName}", blobFileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up failed upload: {BlobFileName}", blobFileName);
        }
    }


    /// <summary>
    /// Uploads a file to the specified container and returns file information.
    /// </summary>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The information of the uploaded file.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     400 Bad Request: When there is no file provided to upload, 
    ///         the Container Name provided is invalid, 
    ///         or the file name is missing
    /// </returns>
    [HttpPost("upload/{containerName}/{fileName}/file-info")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [Produces<ExportedDocumentLinkInfo>]
    [SwaggerIgnore]
    public async Task<IActionResult> UploadFileReturnFileInfo(string containerName, string fileName, [FromForm] IFormFile file)
    {
        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate containerName
        if (!await IsValidContainerNameAsync(containerName))
        {
            return BadRequest("Invalid container name. Must be a valid container.");
        }

        // Validate fileName
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        await using var stream = file.OpenReadStream();
        var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);

        // Deduplication logic for 'reviews' container
        if (containerName == "reviews" && !string.IsNullOrEmpty(exportedDocumentLink.FileHash))
        {
            var existingLink = await _dbContext.ExportedDocumentLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.FileHash == exportedDocumentLink.FileHash && x.Type == FileDocumentType.Review && x.Id != exportedDocumentLink.Id);
            if (existingLink != null)
            {
                // Clean up the newly uploaded file and document link
                try
                {
                    await _fileHelper.DeleteBlobAsync(containerName, blobFileName);
                    _dbContext.ExportedDocumentLinks.Remove(exportedDocumentLink);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up duplicate review file, but will still use existing link");
                }
                var existingInfo = _mapper.Map<ExportedDocumentLinkInfo>(existingLink);
                return Ok(existingInfo);
            }
        }

        var fileInfo = _mapper.Map<ExportedDocumentLinkInfo>(exportedDocumentLink);
        return Ok(fileInfo);
    }

    /// <summary>
    /// Gets the file information by its access URL.
    /// </summary>
    /// <param name="fileAccessUrl">The access URL of the file.</param>
    /// <returns>The information of the file if found, otherwise NotFound.
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file info could not be found using the url provided
    /// </returns>
    [HttpGet("file-info/{fileAccessUrl}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<ExportedDocumentLinkInfo>]
    public async Task<ActionResult<ExportedDocumentLinkInfo>> GetFileInfo(string fileAccessUrl)
    {
        //The asset id is the last part of the URL. It's a Guid. The string may be URL encoded.
        var decodedFileUrl = Uri.UnescapeDataString(fileAccessUrl);
        var assetId = decodedFileUrl.Substring(decodedFileUrl.LastIndexOf('/') + 1);

        var fileInfoModel = await _dbContext.ExportedDocumentLinks
            .AsNoTracking()
            .Include(x => x.GeneratedDocument)
            .FirstOrDefaultAsync(edl => edl.Id == Guid.Parse(assetId));

        if (fileInfoModel == null)
        {
            return NotFound();
        }

        var fileInfo = _mapper.Map<ExportedDocumentLinkInfo>(fileInfoModel);

        return Ok(fileInfo);
    }

    /// <summary>
    /// Uploads a file to the designated upload source for a specific document process.
    /// </summary>
    /// <param name="processShortName">The short name of the document process.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The access URL of the uploaded file.</returns>
    [HttpPost("upload/document-process/{processShortName}/{fileName}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<string>]
    public async Task<IActionResult> UploadFileToDocumentProcessAsync(string processShortName, string fileName, [FromForm] IFormFile? file = null)
    {
        // URL Decode the file name and process name
        fileName = Uri.UnescapeDataString(fileName);
        processShortName = Uri.UnescapeDataString(processShortName);

        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate file name
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        try
        {
            // Get the document process
            var process = await _documentLibraryProcessService.GetDocumentProcessInfoByShortNameAsync(processShortName);
            if (process == null)
            {
                return NotFound($"Document process '{processShortName}' not found.");
            }

            // Find the upload source for this process
            var uploadSource = await GetUploadSourceForProcessAsync(process.Id);
            if (uploadSource == null)
            {
                // Fall back to legacy behavior
                _logger.LogInformation("No upload source configured for process {ProcessName}, falling back to legacy upload", processShortName);
                return await UploadFileToLegacyContainerAsync(process.BlobStorageContainerName, fileName, file);
            }

            return await UploadFileToFileStorageSourceAsync(uploadSource.Value, fileName, file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to document process {ProcessName}", fileName, processShortName);
            return BadRequest($"Error uploading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads a file to the designated upload source for a specific document library.
    /// </summary>
    /// <param name="libraryShortName">The short name of the document library.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The access URL of the uploaded file.</returns>
    [HttpPost("upload/document-library/{libraryShortName}/{fileName}")]
    [RequiresAnyPermission(PermissionKeys.GenerateDocument, PermissionKeys.Chat)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [Produces<string>]
    public async Task<IActionResult> UploadFileToDocumentLibraryAsync(string libraryShortName, string fileName, [FromForm] IFormFile? file = null)
    {
        // URL Decode the file name and library name
        fileName = Uri.UnescapeDataString(fileName);
        libraryShortName = Uri.UnescapeDataString(libraryShortName);

        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate file name
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        try
        {
            // Get the document library
            var library = await _dbContext.DocumentLibraries
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ShortName == libraryShortName);

            if (library == null)
            {
                return NotFound($"Document library '{libraryShortName}' not found.");
            }

            // Find the upload source for this library
            var uploadSource = await GetUploadSourceForLibraryAsync(library.Id);
            if (uploadSource == null)
            {
                // Fall back to legacy behavior
                _logger.LogInformation("No upload source configured for library {LibraryName}, falling back to legacy upload", libraryShortName);
                return await UploadFileToLegacyContainerAsync(library.BlobStorageContainerName, fileName, file);
            }

            return await UploadFileToFileStorageSourceAsync(uploadSource.Value, fileName, file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to document library {LibraryName}", fileName, libraryShortName);
            return BadRequest($"Error uploading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates if the container name is valid.
    /// </summary>
    /// <param name="containerName">The name of the container.</param>
    /// <returns>True if the container name is valid, otherwise false.</returns>
    private async Task<bool> IsValidContainerNameAsync(string containerName)
    {
        if (containerName is
            "document-export" or
            "document-assets" or
            "reviews" or
            "temporary-references" or
            "index-backups")
        {
            return true;
        }

        // Validate dynamically if container name might be part of either a document library or a document process
        var allDocumentProcesses = await _documentLibraryProcessService.GetCombinedDocumentProcessInfoListAsync();
        var documentProcess = allDocumentProcesses.FirstOrDefault(x => x.BlobStorageContainerName == containerName);

        if (documentProcess != null)
        {
            return true;
        }

        var documentLibraryForContainerName = await _dbContext.DocumentLibraries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BlobStorageContainerName == containerName);

        return documentLibraryForContainerName != null;
    }

    /// <summary>
    /// Gets the upload source for a document process.
    /// </summary>
    /// <param name="processId">The document process ID.</param>
    /// <returns>The upload source information or null if none is configured.</returns>
    private async Task<(FileStorageSourceInfo source, string containerName)?> GetUploadSourceForProcessAsync(Guid processId)
    {
        var uploadAssociation = await _dbContext.DocumentProcessFileStorageSources
            .AsNoTracking()
            .Where(dps => dps.DocumentProcessId == processId && dps.AcceptsUploads && dps.IsActive)
            .Include(dps => dps.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .FirstOrDefaultAsync();

        if (uploadAssociation?.FileStorageSource?.FileStorageHost == null)
        {
            return null;
        }

        var sourceInfo = _mapper.Map<FileStorageSourceInfo>(uploadAssociation.FileStorageSource);
        return (sourceInfo, uploadAssociation.FileStorageSource.ContainerOrPath);
    }

    /// <summary>
    /// Gets the upload source for a document library.
    /// </summary>
    /// <param name="libraryId">The document library ID.</param>
    /// <returns>The upload source information or null if none is configured.</returns>
    private async Task<(FileStorageSourceInfo source, string containerName)?> GetUploadSourceForLibraryAsync(Guid libraryId)
    {
        var uploadAssociation = await _dbContext.DocumentLibraryFileStorageSources
            .AsNoTracking()
            .Where(dls => dls.DocumentLibraryId == libraryId && dls.AcceptsUploads && dls.IsActive)
            .Include(dls => dls.FileStorageSource)
            .ThenInclude(s => s.FileStorageHost)
            .FirstOrDefaultAsync();

        if (uploadAssociation?.FileStorageSource?.FileStorageHost == null)
        {
            return null;
        }

        var sourceInfo = _mapper.Map<FileStorageSourceInfo>(uploadAssociation.FileStorageSource);
        return (sourceInfo, uploadAssociation.FileStorageSource.ContainerOrPath);
    }

    /// <summary>
    /// Uploads a file to a specific file storage source.
    /// </summary>
    /// <param name="uploadSourceInfo">The upload source information.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The file access URL.</returns>
    private async Task<IActionResult> UploadFileToFileStorageSourceAsync((FileStorageSourceInfo source, string containerName) uploadSourceInfo, string fileName, IFormFile file)
    {
        var (sourceInfo, containerName) = uploadSourceInfo;

        try
        {
            // Create the appropriate file storage service for this source
            var fileStorageService = _fileStorageServiceFactory.CreateService(sourceInfo);

            _logger.LogInformation("Uploading file {FileName} using {ProviderType} to source {SourceName}",
                fileName, fileStorageService.ProviderType, sourceInfo.Name);

            // Upload the file using the file storage service
            await using var stream = file.OpenReadStream();

            // Use the auto-import folder specified in the source or default to "ingest-auto"
            var folderPath = sourceInfo.AutoImportFolderName ?? "ingest-auto";

            // Upload the file to the storage service
            var relativePath = await fileStorageService.UploadFileAsync(fileName, stream, folderPath);

            // Save file information to database and get access URLs
            var uploadResult = await fileStorageService.SaveFileInfoAsync(relativePath, fileName);

            _logger.LogInformation("Successfully uploaded file {FileName} to {ProviderType} storage. Access URL: {AccessUrl}",
                fileName, fileStorageService.ProviderType, uploadResult.AccessUrl);

            return Ok(uploadResult.AccessUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to file storage source {SourceType}",
                fileName, sourceInfo.FileStorageHost?.ProviderType);
            return BadRequest($"Error uploading file to {sourceInfo.FileStorageHost?.ProviderType} storage: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads a file using the legacy container-based approach.
    /// </summary>
    /// <param name="containerName">The container name.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="file">The file to upload.</param>
    /// <returns>The file access URL.</returns>
    private async Task<IActionResult> UploadFileToLegacyContainerAsync(string containerName, string fileName, IFormFile file)
    {
        // Validate container name using existing logic
        if (!await IsValidContainerNameAsync(containerName))
        {
            return BadRequest("Invalid container name. Must be a valid container.");
        }

        // Use existing upload logic
        await using var stream = file.OpenReadStream();
        var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);
        var fileAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(exportedDocumentLink.Id);

        return Ok(fileAccessUrl);
    }

    /// <summary>
    /// Attempts to retrieve the DisplayFileName from FileAcknowledgmentRecord for the given URL.
    /// </summary>
    /// <param name="fileUrl">The file URL to search for in acknowledgment records.</param>
    /// <returns>The DisplayFileName if found, otherwise null.</returns>
    private async Task<string?> GetDisplayFileNameAsync(string fileUrl)
    {
        try
        {
            // Try to find a FileAcknowledgmentRecord that matches this URL
            var acknowledgment = await _dbContext.FileAcknowledgmentRecords
                .Where(f => f.FileStorageSourceInternalUrl == fileUrl)
                .FirstOrDefaultAsync();

            return acknowledgment?.DisplayFileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve DisplayFileName for URL {FileUrl}", fileUrl);
            return null;
        }
    }
}
