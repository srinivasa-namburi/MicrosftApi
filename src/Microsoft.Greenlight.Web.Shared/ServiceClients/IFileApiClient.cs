using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IFileApiClient : IServiceClient
{
    Task<string> UploadFileDirectAsync(string containerName, string fileName, IBrowserFile file);
    Task<string> UploadFileAndStoreLinkAsync(string containerName, string fileName, IBrowserFile file);
    Task<ContentReferenceItemInfo> UploadTemporaryReferenceFileAsync(string fileName, IBrowserFile file);
    Task<ExportedDocumentLinkInfo?> GetFileInfoById(Guid linkId);
    string GetDownloadUrlForExportedLinkId(Guid linkId, bool? openInline = null);
    string GetDownloadUrlForExternalLinkAsset(Guid assetId, bool? openInline = null);
    string ExtractBlobUrlFromProxiedUrl(string proxiedUrl);
    Task<string> UploadFileToDocumentProcessAsync(string processShortName, string fileName, IBrowserFile file);
    Task<string> UploadFileToDocumentLibraryAsync(string libraryShortName, string fileName, IBrowserFile file);
    Task<string> ResolveFileAcknowledgmentUrlAsync(Guid acknowledgmentId);
}