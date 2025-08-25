// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel;

namespace Microsoft.Greenlight.Shared.Services.Caching;

/// <summary>
/// Centralized application cache abstraction wrapping HybridCache and in-memory caching.
/// Provides size-aware and memory-only options to avoid overloading distributed cache backends like Redis.
/// </summary>
public interface IAppCache
{
    /// <summary>
    /// Gets or creates a cached value with stampede protection.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="factory">Factory to produce the value on miss.</param>
    /// <param name="ttl">Time to live.</param>
    /// <param name="allowDistributed">When false, stores only in-process and skips distributed backends.</param>
    /// <param name="token">Cancellation token.</param>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default);

    /// <summary>
    /// Sets a value in cache.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    /// <param name="ttl">Time to live.</param>
    /// <param name="allowDistributed">When false, stores only in-process and skips distributed backends.</param>
    /// <param name="token">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default);

    /// <summary>
    /// Removes a key from cache (best-effort).
    /// </summary>
    Task RemoveAsync(string key, CancellationToken token = default);
}