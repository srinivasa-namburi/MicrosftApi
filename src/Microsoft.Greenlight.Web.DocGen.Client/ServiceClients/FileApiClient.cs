using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net;
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

    public async Task<ExportedDocumentLinkInfo?> GetFileInfoById(Guid linkId)
    {
        var encodedLinkId = linkId.ToString();
        var url = $"/api/file/file-info/{encodedLinkId}";
    
        var response = await SendGetRequestMessage(url);
    
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    
        try
        {
            response?.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving file info for link ID {LinkId}", linkId);
            return null;
        }
    
        return await response.Content.ReadFromJsonAsync<ExportedDocumentLinkInfo>();
    }

    public string GetDownloadUrlForExportedLinkId(Guid linkId)
    {
        return $"{HttpClient.BaseAddress}api/file/download/asset/{linkId}";
    }

    public string ExtractBlobUrlFromProxiedUrl(string proxiedUrl)
    {
       // Remove /api/file/download/
        var cleanedUrl = proxiedUrl.Replace("/api/file/download/", string.Empty);

        // WebUtility.UrlDecode the cleaned URL
        cleanedUrl = WebUtility.UrlDecode(cleanedUrl);
        return cleanedUrl;
    }
}