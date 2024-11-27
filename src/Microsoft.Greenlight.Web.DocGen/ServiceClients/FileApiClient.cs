using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

public class FileApiClient : BaseServiceClient<FileApiClient>, IFileApiClient
{
    public FileApiClient(HttpClient httpClient, ILogger<FileApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string> UploadFileDirectAsync(string containerName, string fileName, IBrowserFile file)
    {
        var url = $"api/file/{containerName}/{fileName}/direct";
        return await UploadFileAsync(url, file);
    }

    public async Task<string> UploadFileAndStoreLinkAsync(string containerName, string fileName, IBrowserFile file)
    {
        var url = $"api/file/{containerName}/{fileName}";
        return await UploadFileAsync(url, file);
    }

    private async Task<string> UploadFileAsync(string url, IBrowserFile file)
    {
        // The server edition of this expects an IFormFile, not an IBrowserFile. Create a new IFormFile from the IBrowserFile.
        var fileStream = file.OpenReadStream();
        var fileContent = new StreamContent(fileStream);
        var finalFile = new MultipartFormDataContent
        {
            { fileContent, "file", file.Name }
        };

        var result = await SendPostRequestMessage(url, finalFile);

        result?.EnsureSuccessStatusCode();

        return await result.Content.ReadAsStringAsync();

    }
}