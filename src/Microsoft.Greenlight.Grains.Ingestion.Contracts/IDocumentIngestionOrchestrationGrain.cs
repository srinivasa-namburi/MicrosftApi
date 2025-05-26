using System.Threading.Tasks;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Grain contract for orchestrating document ingestion.
    /// </summary>
    public interface IDocumentIngestionOrchestrationGrain : IGrainWithStringKey
    {
        Task StartIngestionAsync(
            string documentLibraryShortName,
            DocumentLibraryType documentLibraryType,
            string blobContainerName,
            string folderPath);

        Task OnIngestionCompletedAsync();
        Task OnIngestionFailedAsync(string reason, bool acquired);
    }
}