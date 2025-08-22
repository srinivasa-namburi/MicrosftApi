// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Models;

namespace Microsoft.Greenlight.Grains.Ingestion.Contracts
{
    /// <summary>
    /// Grain contract for computing blob hashes for a given container/prefix.
    /// Prevents concurrent runs per orchestration and supports parallel hashing.
    /// </summary>
    public interface IBlobHashingGrain : IGrainWithStringKey
    {
        /// <summary>
        /// Returns true if this hashing grain is currently computing hashes.
        /// </summary>
        Task<bool> IsActiveAsync();

        /// <summary>
        /// Starts hashing for the specified container and folder prefix. Returns hash info for discovered blobs.
        /// </summary>
        /// <param name="container">The blob container name.</param>
        /// <param name="folderPrefix">The folder/prefix to enumerate.</param>
        /// <param name="runId">The run id of the owning orchestration.</param>
        /// <returns>List of blob hash info entries.</returns>
        Task<List<BlobHashInfo>> StartHashingAsync(string container, string folderPrefix, Guid runId);
    }
}
