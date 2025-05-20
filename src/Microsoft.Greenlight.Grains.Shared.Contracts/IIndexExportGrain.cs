using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts
{
    /// <summary>
    /// Interface for exporting Postgres table data to blob storage.
    /// </summary>
    public interface IIndexExportGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Starts an export job for the specified table.
        /// </summary>
        /// <param name="schema">The database schema.</param>
        /// <param name="tableName">The table to export.</param>
        /// <param name="userGroup">The user or group that should receive notifications.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task StartExportAsync(string schema, string tableName, string userGroup);
        
        /// <summary>
        /// Gets the current status of an export job.
        /// </summary>
        /// <returns>The job status.</returns>
        Task<IndexExportJobStatus> GetStatusAsync();
    }
}