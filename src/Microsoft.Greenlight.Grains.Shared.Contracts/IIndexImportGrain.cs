using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts
{
    /// <summary>
    /// Interface for importing Postgres table data from blob storage.
    /// </summary>
    public interface IIndexImportGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Starts an import job for the specified table.
        /// </summary>
        /// <param name="schema">The database schema.</param>
        /// <param name="tableName">The table to import into.</param>
        /// <param name="blobUrl">The URL of the blob containing the import data.</param>
        /// <param name="userGroup">The user or group that should receive notifications.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartImportAsync(string schema, string tableName, string blobUrl, string userGroup);
        
        /// <summary>
        /// Gets the current status of an import job.
        /// </summary>
        /// <returns>The job status.</returns>
        Task<IndexImportJobStatus> GetStatusAsync();
    }
}