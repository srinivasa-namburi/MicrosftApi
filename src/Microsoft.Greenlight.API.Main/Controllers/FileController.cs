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

    /// <summary>
    /// Initializes a new instance of the <see cref="FileController"/> class.
    /// </summary>
    /// <param name="fileHelper">The file helper for Azure operations.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="documentLibraryProcessService">The document library process service.</param>
    public FileController(
        AzureFileHelper fileHelper,
        DocGenerationDbContext dbContext,
        IMapper mapper,
        IDocumentProcessInfoService documentLibraryProcessService
    )
    {
        _fileHelper = fileHelper;
        _dbContext = dbContext;
        _mapper = mapper;
        _documentLibraryProcessService = documentLibraryProcessService;
    }

    /// <summary>
    /// Downloads a file from the specified URL.
    /// </summary>
    /// <param name="fileUrl">The URL of the file to download.</param>
    /// <returns>The file stream if found
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file can't be found using the file url provided
    /// </returns>
    [HttpGet("download/{fileUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadFile(string fileUrl)
    {
        var decodedFileUrl = Uri.UnescapeDataString(fileUrl);

        var stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(decodedFileUrl);
        if (stream == null)
        {
            return NotFound();
        }

        // Optional: Set the content type if you know it. Here, application/octet-stream
        // is a generic type for binary data.
        var contentType = new MimeTypesDetection().GetFileType(decodedFileUrl);
        // the file name is everything after the final '/' character. It consists of the
        // Document ID, a dash, a random GUID and then the file extension after a period.
        var fileName = decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// Downloads a file by its link ID.
    /// </summary>
    /// <param name="linkId">The link ID of the file to download.</param>
    /// <returns>The file stream if found
    /// Produces Status Codes:
    ///     200 OK: When completed sucessfully
    ///     404 Not Found: When the file can't be found using the file url provided
    /// </returns>
    [HttpGet("download/asset/{linkId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> DownloadFileById(string linkId)
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

        // Optional: Set the content type if you know it. Here, application/octet-stream is a
        // generic type for binary data.
        var contentType = new MimeTypesDetection().GetFileType(decodedFileUrl);
        var fileName = decodedFileUrl[(decodedFileUrl.LastIndexOf('/') + 1)..];
        // the file name is everything after the final '/' character. It consists of the Document ID,
        // a dash, a random GUID and then the file extension after a period.
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

        // Read the file stream
        await using var stream = file.OpenReadStream();

        // Generate a random file name for the backend in blob storage
        var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);

        // Upload the file to blob storage
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);

        // Save the file information in the database
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);

        // Create a content reference item
        var contentReferenceItem = new ContentReferenceItem
        {
            Id = Guid.NewGuid(),
            ContentReferenceSourceId = exportedDocumentLink.Id,
            ReferenceType = ContentReferenceType.ExternalFile,
            DisplayName = fileName,
            Description = $"Uploaded document: {fileName}"
        };

        _dbContext.ContentReferenceItems.Add(contentReferenceItem);
        await _dbContext.SaveChangesAsync();

        // Return the content reference info
        var contentReferenceItemInfo = _mapper.Map<ContentReferenceItemInfo>(contentReferenceItem);

        return Ok(contentReferenceItemInfo);
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

        // Read the file stream
        await using var stream = file.OpenReadStream();
        // Generate a random file name for the backend in blob storage
        var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);
        // Upload the file to the blob storage
        var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);
        // Save the file information in the database
        var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);
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
            "temporary-references")
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
