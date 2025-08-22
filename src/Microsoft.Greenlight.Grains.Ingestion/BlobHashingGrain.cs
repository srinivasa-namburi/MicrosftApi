// Copyright (c) Microsoft Corporation. All rights reserved.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts.Models;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Helpers;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion
{
    /// <summary>
    /// Grain that computes hashes for blobs in a given container/prefix, with a throttle, and avoids concurrent runs.
    /// Grain key should be the orchestration id to ensure one hashing run per orchestration.
    /// </summary>
    [Reentrant]
    public class BlobHashingGrain : Grain, IBlobHashingGrain
    {
        private readonly ILogger<BlobHashingGrain> _logger;
        private readonly AzureFileHelper _azureFileHelper;
        private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;

        private volatile bool _isActive;

        public BlobHashingGrain(
            ILogger<BlobHashingGrain> logger,
            AzureFileHelper azureFileHelper,
            IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot)
        {
            _logger = logger;
            _azureFileHelper = azureFileHelper;
            _optionsSnapshot = optionsSnapshot;
        }

        public Task<bool> IsActiveAsync() => Task.FromResult(_isActive);

        public async Task<List<BlobHashInfo>> StartHashingAsync(string container, string folderPrefix, Guid runId)
        {
            if (_isActive)
            {
                _logger.LogInformation("[BlobHashing] Already active for {Key}, skipping.", this.GetPrimaryKeyString());
                return new List<BlobHashInfo>();
            }

            _isActive = true;
            try
            {
                _logger.LogInformation("[BlobHashing] Start hashing for container={Container}, prefix={Prefix}, runId={RunId}", container, folderPrefix, runId);

                // Decide parallelism based on NumberOfIngestionWorkers, with a small multiplier but cap it
                var workers = _optionsSnapshot.Value.GreenlightServices.Scalability.NumberOfIngestionWorkers;
                if (workers <= 0) workers = 2;
                var maxParallel = Math.Clamp(workers * 2, 2, 16);
                using var throttle = new SemaphoreSlim(maxParallel, maxParallel);

                var containerClient = _azureFileHelper.GetBlobServiceClient().GetBlobContainerClient(container);
                await containerClient.CreateIfNotExistsAsync();

                // Pre-enumerate eligible blobs to know total for progress tracking
                var eligible = containerClient
                    .GetBlobs(prefix: folderPrefix)
                    .Select(b => b.Name)
                    .Where(blobName =>
                    {
                        string relativeFileName = Path.GetFileName(blobName);
                        return !string.IsNullOrEmpty(relativeFileName) &&
                               !blobName.TrimEnd('/').Equals(folderPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                int total = eligible.Count;
                if (total == 0)
                {
                    _logger.LogInformation("[BlobHashing] No blobs found under {Container}/{Prefix}", container, folderPrefix);
                    return new List<BlobHashInfo>();
                }

                _logger.LogInformation("[BlobHashing] Hashing {Total} blobs with maxParallel={MaxParallel}", total, maxParallel);

                var results = new ConcurrentBag<BlobHashInfo>();
                var tasks = new List<Task>(capacity: total);

                // Progress tracking state
                int completed = 0;
                int lastLoggedMilestone = 0; // percentage milestone already logged (0..100)

                foreach (var blobName in eligible)
                {
                    string relativeFileName = Path.GetFileName(blobName);
                    var fullUrl = containerClient.GetBlobClient(blobName).Uri.ToString();

                    await throttle.WaitAsync();
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            string? hash = await _azureFileHelper.GenerateFileHashFromBlobUrlAsync(fullUrl);
                            results.Add(new BlobHashInfo
                            {
                                BlobName = blobName,
                                RelativeFileName = relativeFileName,
                                FullBlobUrl = fullUrl,
                                Hash = hash
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[BlobHashing] Failed hashing for {Blob}", fullUrl);
                            results.Add(new BlobHashInfo
                            {
                                BlobName = blobName,
                                RelativeFileName = relativeFileName,
                                FullBlobUrl = fullUrl,
                                Hash = null
                            });
                        }
                        finally
                        {
                            try { throttle.Release(); } catch { }

                            // Update progress and log every 10%
                            var done = Interlocked.Increment(ref completed);
                            int percent = (int)Math.Round(done * 100.0 / total);
                            // Snap to 10% milestones
                            int milestone = Math.Min(100, (percent / 10) * 10);

                            int prev;
                            do
                            {
                                prev = Volatile.Read(ref lastLoggedMilestone);
                                if (milestone <= prev)
                                {
                                    break; // already logged or not advanced to next 10%
                                }
                            }
                            while (Interlocked.CompareExchange(ref lastLoggedMilestone, milestone, prev) != prev);

                            if (milestone > prev)
                            {
                                _logger.LogInformation("[BlobHashing] Progress: {Percent}% ({Done}/{Total})", milestone, done, total);
                            }
                        }
                    });
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                _logger.LogInformation("[BlobHashing] Completed hashing for {Count} blobs", results.Count);
                return results.ToList();
            }
            finally
            {
                _isActive = false;
            }
        }
    }
}
