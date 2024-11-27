using AutoMapper;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.API.Main.Controllers;

[Route("/api/file")]
public class FileController : BaseController
{
    private readonly AzureFileHelper _fileHelper;
    private readonly DocGenerationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;
    private readonly IDocumentProcessInfoService _documentLibraryProcessService;

    public FileController(
        AzureFileHelper fileHelper, 
        DocGenerationDbContext dbContext, 
        IMapper mapper,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IDocumentProcessInfoService documentLibraryProcessService)
    {
        _fileHelper = fileHelper;
        _dbContext = dbContext;
        _mapper = mapper;
        _documentLibraryInfoService = documentLibraryInfoService;
        _documentLibraryProcessService = documentLibraryProcessService;
    }

    [HttpGet("download/{fileUrl}")]
    public async Task<IActionResult> DownloadFile(string fileUrl)
    {
        var decodedFileUrl = Uri.UnescapeDataString(fileUrl);

        var stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(decodedFileUrl);
        if (stream == null)
        {
            return NotFound();
        }

        // Optional: Set the content type if you know it. Here, application/octet-stream is a generic type for binary data.
        var contentType = new MimeTypesDetection().GetFileType(decodedFileUrl);
        
        // the file name is everything after the final '/' character. It consists of the Document ID, a dash, a random GUID and then the file extension after a period.
        var fileName = decodedFileUrl.Substring(decodedFileUrl.LastIndexOf('/') + 1);
        return File(stream, contentType, fileName);
    }

    [HttpGet("download/asset/{linkId}")]
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

        // Optional: Set the content type if you know it. Here, application/octet-stream is a generic type for binary data.
        var contentType = new MimeTypesDetection().GetFileType(decodedFileUrl);

        // the file name is everything after the final '/' character. It consists of the Document ID, a dash, a random GUID and then the file extension after a period.
        var fileName = decodedFileUrl.Substring(decodedFileUrl.LastIndexOf('/') + 1);
        return File(stream, contentType, fileName);
    }

    [HttpPost("upload/{containerName}/{fileName}")]
    [HttpPost("upload/{containerName}/{fileName}/direct")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(string))]
    public async Task<IActionResult> UploadFile(string containerName, string fileName, [FromForm] IFormFile? file = null)
    {
        // Url Decode the file name and container name
        fileName = Uri.UnescapeDataString(fileName);
        containerName = Uri.UnescapeDataString(containerName);

        var directUpload = Request.GetDisplayUrl().Contains("/direct");

        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate containerName
        if (containerName != "document-export" && containerName != "document-assets" && containerName != "reviews")
        {
            var badContainerName = true;
            // Validate dynamically if container name might be part of either a document library or a document process

            var allDocumentProcesses =
                await _documentLibraryProcessService.GetCombinedDocumentProcessInfoListAsync();

            var documentProcess = allDocumentProcesses.FirstOrDefault(x => x.BlobStorageContainerName == containerName);

            if (documentProcess != null)
            {
                badContainerName = false;
            }
            else
            {
                var documentLibraryForContainerName = await _dbContext.DocumentLibraries
                    .Where(x => x.BlobStorageContainerName == containerName)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (documentLibraryForContainerName != null)
                {
                    badContainerName = false;
                }
            }

            if (badContainerName)
            {
                return BadRequest("Invalid container name. Must be one of 'document-export', 'document-assets', or 'reviews'.");
            }
        }

        // Validate fileName
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        // Read the file stream
        using (var stream = file.OpenReadStream())
        {
            string blobFileName;
            if (directUpload)
            {
                blobFileName = fileName;
            }
            else
            {
                // Generate a random file name for the backend in blob storage
                blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);
            }
            
            // Upload the file to the blob storage
            var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);

            string fileAccessUrl;
            if (directUpload)
            {
                fileAccessUrl = blobUrl;
                fileAccessUrl = _fileHelper.GetProxiedBlobUrl(fileAccessUrl);
            }
            else
            {
                // Save the file information in the database
                var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);

                // Get the access URL for the file
                fileAccessUrl = _fileHelper.GetProxiedAssetBlobUrl(exportedDocumentLink.Id);
            }

            return Ok(fileAccessUrl);
        }
    }

    [HttpPost("upload/{containerName}/{fileName}/file-info")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces(typeof(ExportedDocumentLinkInfo))]
    public async Task<IActionResult> UploadFileReturnFileInfo(string containerName, string fileName, [FromForm] IFormFile file)
    {
        // Check if the file is provided
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        // Validate containerName
        if (containerName != "document-export" && containerName != "document-assets" && containerName != "reviews")
        {
            return BadRequest("Invalid container name. Must be one of 'document-export', 'document-assets', or 'reviews'.");
        }

        // Validate fileName
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Invalid file name.");
        }

        // Read the file stream
        await using (var stream = file.OpenReadStream())
        {
            // Generate a random file name for the backend in blob storage
            var blobFileName = Guid.NewGuid() + Path.GetExtension(fileName);

            // Upload the file to the blob storage
            var blobUrl = await _fileHelper.UploadFileToBlobAsync(stream, blobFileName, containerName, true);

            // Save the file information in the database
            var exportedDocumentLink = await _fileHelper.SaveFileInfoAsync(blobUrl, containerName, fileName);
            
            var fileInfo = _mapper.Map<ExportedDocumentLinkInfo>(exportedDocumentLink);

            return Ok(fileInfo);
        }
    }

    [HttpGet("file-info/{fileAccessUrl}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces(typeof(ExportedDocumentLinkInfo))]
    public async Task<ActionResult<ExportedDocumentLinkInfo>> GetFileInfo(string fileAccessUrl)
    {
        //The asset id is the last part of the URL. It's a Guid. The string may be URL encoded.

        
        var decodedFileUrl = Uri.UnescapeDataString(fileAccessUrl);
        var assetId = decodedFileUrl.Substring(decodedFileUrl.LastIndexOf('/') + 1);
        
        var fileInfoModel = await _dbContext.ExportedDocumentLinks
            .AsNoTracking()
            .Include(x=>x.GeneratedDocument)
            .FirstOrDefaultAsync(edl => edl.Id == Guid.Parse(assetId));

        if (fileInfoModel == null)
        {
            return NotFound();
        }

        var fileInfo = _mapper.Map<ExportedDocumentLinkInfo>(fileInfoModel);

        return Ok(fileInfo);
    }
}
