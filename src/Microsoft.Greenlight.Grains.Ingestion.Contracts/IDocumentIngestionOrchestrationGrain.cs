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
        /// <summary>
        /// Starts or resumes ingestion for a specific container/folder orchestration.
        /// </summary>
        Task StartIngestionAsync(
            string documentLibraryShortName,
            DocumentLibraryType documentLibraryType,
            string blobContainerName,
            string folderPath);

        /// <summary>
        /// Called by file grains when a file has completed processing successfully.
        /// </summary>
        Task OnIngestionCompletedAsync();

        /// <summary>
        /// Called by file grains when a file has failed processing.
        /// </summary>
        Task OnIngestionFailedAsync(string reason, bool acquired);

        /// <summary>
        /// Returns true if this orchestration is currently running or has pending work.
        /// </summary>
        Task<bool> IsRunningAsync();

        /// <summary>
        /// Requests this orchestration grain to deactivate as soon as possible.
        /// Used during cleanup (e.g., library deletion) to avoid stale activations.
        /// </summary>
        Task DeactivateAsync();

        /// <summary>
        /// Diagnostic method to check for stuck documents and attempt recovery.
        /// </summary>
        Task CheckAndRecoverStuckDocumentsAsync();
    }
}