using Microsoft.AspNetCore.Components.Forms;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IFileApiClient : IServiceClient
{
    Task<string> UploadFileDirectAsync(string containerName, string fileName, IBrowserFile file);
    Task<string> UploadFileAndStoreLinkAsync(string containerName, string fileName, IBrowserFile file);
}