using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net.Http.Json;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class FileApiClient : WebAssemblyBaseServiceClient<FileApiClient>, IFileApiClient
{
    public FileApiClient(HttpClient httpClient, ILogger<FileApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string> UploadFileDirectAsync(string containerName, string fileName, IBrowserFile file)
    {
        var encodedFileName = Uri.EscapeDataString(fileName);
        var encodedContainerName = Uri.EscapeDataString(containerName);
        var url = $"/api/file/upload/direct/{encodedContainerName}/{encodedFileName}";
        return await UploadFileAsync(url, file);
    }

    public async Task<string> UploadFileAndStoreLinkAsync(string containerName, string fileName, IBrowserFile file)
    {
        var encodedFileName = Uri.EscapeDataString(fileName);
        var encodedContainerName = Uri.EscapeDataString(containerName);
        var url = $"/api/file/upload/{encodedContainerName}/{encodedFileName}";
        return await UploadFileAsync(url, file);
    }

    private async Task<string> UploadFileAsync(string url, IBrowserFile file)
    {
        var result = await SendPostRequestMessage(url, file);

        result?.EnsureSuccessStatusCode();

        return await result.Content.ReadAsStringAsync();

    }

    public async Task<ContentReferenceItemInfo> UploadTemporaryReferenceFileAsync(string fileName, IBrowserFile file)
    {
        var encodedFileName = Uri.EscapeDataString(fileName);
        var url = $"/api/file/upload/reference/{encodedFileName}";
    
        var result = await SendPostRequestMessage(url, file);
        result?.EnsureSuccessStatusCode();

        var responseContent = await result!.Content.ReadFromJsonAsync<ContentReferenceItemInfo>();
        return responseContent!;

    }
}