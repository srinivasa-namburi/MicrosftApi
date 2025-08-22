using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts
{
    /// <summary>
    /// Runs a repair job that migrates old vector-store document IDs to the canonical Base64Url-encoded IDs.
    /// Heavy job: intended to run once on startup and occasionally thereafter.
    /// </summary>
    public interface IVectorStoreIdFixGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Executes the repair job across all SK Vector Store document processes and libraries.
        /// </summary>
        Task ExecuteAsync();
    }
}
