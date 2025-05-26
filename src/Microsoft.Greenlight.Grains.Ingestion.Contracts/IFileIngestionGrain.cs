// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Threading.Tasks;
using Orleans;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Grain contract for tracking ingestion state and progress for a single file.
    /// </summary>
    public interface IFileIngestionGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Starts or resumes ingestion for the specified file.
        /// </summary>
        Task StartIngestionAsync(Guid documentId);

        /// <summary>
        /// Updates the ingestion state for the file.
        /// </summary>
        Task UpdateStateAsync(Guid documentId);

        /// <summary>
        /// Gets the current state of the file ingestion.
        /// </summary>
        Task<Guid> GetStateIdAsync();

        /// <summary>
        /// Notifies the grain that processing is complete.
        /// </summary>
        Task MarkCompleteAsync();

        /// <summary>
        /// Notifies the grain that ingestion has failed.
        /// </summary>
        Task MarkFailedAsync(string error);

        /// <summary>
        /// Callback for when file copy is completed.
        /// </summary>
        Task OnFileCopyCompletedAsync();

        /// <summary>
        /// Callback for when file copy has failed.
        /// </summary>
        Task OnFileCopyFailedAsync(string error);

        /// <summary>
        /// Callback for when processing is completed.
        /// </summary>
        Task OnProcessingCompletedAsync();

        /// <summary>
        /// Callback for when processing has failed.
        /// </summary>
        Task OnProcessingFailedAsync(string error);

        /// <summary>
        /// Returns true if this grain is active and the file is not in a terminal state (Complete/Failed).
        /// </summary>
        Task<bool> IsActiveAsync();
    }
}
