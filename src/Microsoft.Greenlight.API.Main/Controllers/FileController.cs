using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.KernelMemory.Pipeline;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Models.Authorization;

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
    private readonly ILogger<FileController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileController"/> class.
    /// </summary>
    /// <param name="fileHelper">The file helper for Azure operations.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="documentLibraryProcessService">The document library process service.</param>
    /// <param name="logger"></param>
    public FileController(
        AzureFileHelper fileHelper,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IDocumentProcessInfoService documentLibraryProcessService,
        ILogger<FileController> logger)
    {
        _fileHelper = fileHelper;
        _dbContext = dbContext;
        _mapper = mapper;
        _documentLibraryProcessService = documentLibraryProcessService;
        _logger = logger;
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

        var fileName = decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];

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
        var fileName = decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];

        if (!string.IsNullOrWhiteSpace(disposition) && disposition.Equals("inline", StringComparison.OrdinalIgnoreCase))
        {
            // The optional 'page' query is intentionally ignored by the server; viewers can use URL fragments.
            return File(stream, contentType);
        }

        return File(stream, contentType, fileName);
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
        // URL Decode the file name
        fileName = Uri.UnescapeDataString(fileName);
        const string containerName = "temporary-references";

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

        ContentReferenceItem contentReferenceItem;
        ExportedDocumentLink exportedDocumentLink;
        string blobUrl;
        string blobFileName = string.Empty;

        try
        {
            // Read the file stream
            await using var stream = file.OpenReadStream();

            // Generate a random file name for the backend in blob storage
            blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);

            // Upload the file to blob storage
            blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);

            // Save the file information in the database
            exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);

            // Safely try to deduplicate based on file hash - handle any failures gracefully
            try
            {
                if (!string.IsNullOrEmpty(exportedDocumentLink.FileHash))
                {
                    var existingReferences = await _dbContext.ContentReferenceItems
                        .Where(r => r.ReferenceType == ContentReferenceType.ExternalFile)
                        .Join(_dbContext.ExportedDocumentLinks,
                            r => r.ContentReferenceSourceId,
                            e => e.Id,
                            (r, e) => new { Reference = r, ExportedDoc = e })
                        .Where(j => j.ExportedDoc.FileHash == exportedDocumentLink.FileHash &&
                                    j.ExportedDoc.Id != exportedDocumentLink.Id)
                        .Select(j => j.Reference)
                        .ToListAsync();

                    if (existingReferences != null && existingReferences.Any())
                    {
                        // Use the existing reference instead (deduplicate)
                        var existingRef = existingReferences.First();

                        // Log the duplicate detection
                        _logger.LogInformation(
                            "Found duplicate file reference. New URL: {NewUrl}, using existing reference: {ExistingId} with same hash {FileHash}",
                            blobUrl, existingRef.Id, exportedDocumentLink.FileHash);

                        try
                        {
                            // Delete the newly uploaded file and document link since we're using the existing one
                            await _fileHelper.DeleteBlobAsync(containerName, blobFileName);
                            _dbContext.ExportedDocumentLinks.Remove(exportedDocumentLink);
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - we can still use the existing reference
                            _logger.LogWarning(ex, "Error cleaning up duplicate file, but will still use existing reference");
                        }

                        // Return the existing reference
                        var existingReferenceInfo = _mapper.Map<ContentReferenceItemInfo>(existingRef);
                        return Ok(existingReferenceInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                // If anything goes wrong with deduplication, log and continue with creating a new reference
                _logger.LogWarning(ex, "Error during file deduplication check - will create new reference anyway");
                // No return - we'll fall through to the normal creation flow
            }

            // If no duplicate found or deduplication failed, continue with creating a new reference
            contentReferenceItem = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ContentReferenceSourceId = exportedDocumentLink.Id,
                ReferenceType = ContentReferenceType.ExternalFile,
                DisplayName = fileName,
                Description = $"Uploaded document: {fileName}",
                FileHash = exportedDocumentLink.FileHash // This might be null, which is fine
            };

            _dbContext.ContentReferenceItems.Add(contentReferenceItem);
            await _dbContext.SaveChangesAsync();

            // Return the content reference info
            var contentReferenceItemInfo = _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);
            return Ok(contentReferenceItemInfo);
        }
        catch (Exception ex)
        {
            // Log the exception
            _logger.LogError(ex, "Error uploading file {FileName}", fileName);

            // Try to clean up any resources if possible
            try
            {
                if (!string.IsNullOrEmpty(blobFileName))
                {
                    await _fileHelper.DeleteBlobAsync(containerName, blobFileName);
                }
            }
            catch
            {
                // Suppress any exception during cleanup
            }

            // Create a new ContentReferenceItem without file hash as a fallback
            contentReferenceItem = new ContentReferenceItem
            {
                Id = Guid.NewGuid(),
                ReferenceType = ContentReferenceType.ExternalFile,
                DisplayName = fileName,
                Description = $"Uploaded document: {fileName}",
                // No ContentReferenceSourceId or FileHash since we couldn't process the file
            };

            _dbContext.ContentReferenceItems.Add(contentReferenceItem);
            await _dbContext.SaveChangesAsync();

            // Return the content reference info - at least we can give the user something
            var contentReferenceItemInfo = _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);
            return Ok(contentReferenceItemInfo);
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
}
