using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IFileApiClient : IServiceClient
{
    Task<string> UploadFileDirectAsync(string containerName, string fileName, IBrowserFile file);
    Task<string> UploadFileAndStoreLinkAsync(string containerName, string fileName, IBrowserFile file);
    Task<ContentReferenceItemInfo> UploadTemporaryReferenceFileAsync(string fileName, IBrowserFile file);
    Task<ExportedDocumentLinkInfo?> GetFileInfoById(Guid linkId);
    string GetDownloadUrlForExportedLinkId(Guid linkId);
}