using Microsoft.AspNetCore.Mvc;
using ProjectVico.V2.Shared.Helpers;

namespace ProjectVico.V2.API.Main.Controllers;

public class FileController : BaseController
{
    private readonly AzureFileHelper _filehelper;

    public FileController(AzureFileHelper filehelper)
    {
        _filehelper = filehelper;
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadFile(string fileUrl)
    {
        var stream = await _filehelper.GetFileAsStreamFromFullBlobUrlAsync(fileUrl);
        if (stream == null)
        {
            return NotFound();
        }

        // Optional: Set the content type if you know it. Here, application/octet-stream is a generic type for binary data.
        var contentType = "application/octet-stream";
        var fileName = Path.GetFileName(new Uri(fileUrl).LocalPath);

        return File(stream, contentType, fileName);
    }
}