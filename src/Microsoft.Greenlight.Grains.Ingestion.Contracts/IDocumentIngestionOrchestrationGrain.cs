using System.Threading.Tasks;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Grain contract for orchestrating document ingestion.
    /// </summary>
    public interface IDocumentIngestionOrchestrationGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Starts or resumes ingestion for a specific container/folder orchestration.
        /// This method coordinates multiple file ingestion operations that may queue for extended periods.
        /// </summary>
        [ResponseTimeout("2.00:00:00")] // 2 days timeout to handle long-running ingestion orchestrations
        Task StartIngestionAsync(
            string documentLibraryShortName,
            DocumentLibraryType documentLibraryType,
            string blobContainerName,
            string folderPath);

        /// <summary>
        /// Starts or resumes ingestion for a FileStorageSource, handling multiple DL/DPs that use it.
        /// This is the efficient approach for large repositories.
        /// This method coordinates multiple file ingestion operations that may queue for extended periods.
        /// </summary>
        [ResponseTimeout("2.00:00:00")] // 2 days timeout to handle long-running ingestion orchestrations
        Task StartIngestionAsync(
            Guid fileStorageSourceId,
            List<(string shortName, Guid id, DocumentLibraryType type, bool isDocumentLibrary)> dlDpList,
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

        /// <summary>
        /// Forces a reset of the orchestration's active state, clearing any stuck flags.
        /// Used for recovery scenarios during scheduler startup.
        /// </summary>
        Task ForceResetAsync();
    }
}