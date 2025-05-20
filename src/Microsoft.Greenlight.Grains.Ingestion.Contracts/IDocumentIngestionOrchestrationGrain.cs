using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    public interface IDocumentIngestionOrchestrationGrain : IGrainWithGuidKey
    {
        Task StartIngestionAsync(
            string documentLibraryShortName, 
            DocumentLibraryType documentLibraryType,
            string blobContainerName,
            string folderPath);
        
        Task OnFileCopiedAsync(string fileName, string originalDocumentUrl);
        Task OnIngestionCompletedAsync();
        Task OnIngestionFailedAsync(string reason, bool acquired);
    }
}