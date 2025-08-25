// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Services.Caching;

/// <summary>
/// Default implementation for IAppCache using HybridCache with Redis as L2 when available.
/// Adds safeguards to avoid storing very large payloads in distributed cache and handles OOM gracefully.
/// </summary>
public class AppCache : IAppCache
{
    private readonly HybridCache _hybridCache;
    private readonly ILogger<AppCache> _logger;
    private readonly IDistributedCache? _distributed; // optional, used by HybridCache internally

    // default limits: avoid pushing huge values to Redis
    private const int MaxBytesForDistributed = 256 * 1024; // 256 KB

    public AppCache(HybridCache hybridCache, ILogger<AppCache> logger, IDistributedCache? distributed = null)
    {
        _hybridCache = hybridCache;
        _logger = logger;
        _distributed = distributed;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default)
    {
        try
        {
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = allowDistributed ? HybridCacheEntryFlags.None : HybridCacheEntryFlags.DisableDistributedCache
            };

            return await _hybridCache.GetOrCreateAsync<T>(
                key,
                async cancel => await factory(cancel),
                options,
                tags: null,
                cancellationToken: token);
        }
        catch (RedisServerException rsex) when (IsRedisOom(rsex))
        {
            _logger.LogWarning(rsex, "Redis OOM for key {Key}; retrying with local-only and scheduling best-effort purge", key);
            await PurgeNamespaceBestEffortAsync(GetNamespaceFromKey(key));
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = HybridCacheEntryFlags.DisableDistributedCache
            };
            return await _hybridCache.GetOrCreateAsync<T>(
                key,
                async cancel => await factory(cancel),
                options,
                tags: null,
                cancellationToken: token);
        }
        catch (RedisConnectionException rce)
        {
            _logger.LogWarning(rce, "Redis connection issue for key {Key}; using local-only fallback", key);
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = HybridCacheEntryFlags.DisableDistributedCache
            };
            return await _hybridCache.GetOrCreateAsync<T>(
                key,
                async cancel => await factory(cancel),
                options,
                tags: null,
                cancellationToken: token);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default)
    {
        try
        {
            var flags = allowDistributed ? HybridCacheEntryFlags.None : HybridCacheEntryFlags.DisableDistributedCache;
            if (allowDistributed && IsTooLargeForDistributed(value))
            {
                flags |= HybridCacheEntryFlags.DisableDistributedCacheWrite;
            }

            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = flags
            };

            await _hybridCache.SetAsync(key, value!, options, tags: null, cancellationToken: token);
        }
        catch (RedisServerException rsex) when (IsRedisOom(rsex))
        {
            _logger.LogWarning(rsex, "Redis OOM on Set for key {Key}; retrying local-only and scheduling best-effort purge", key);
            await PurgeNamespaceBestEffortAsync(GetNamespaceFromKey(key));
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = HybridCacheEntryFlags.DisableDistributedCache
            };
            await _hybridCache.SetAsync(key, value!, options, tags: null, cancellationToken: token);
        }
        catch (RedisConnectionException rce)
        {
            _logger.LogWarning(rce, "Redis connection issue on Set for key {Key}; using local-only fallback", key);
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = HybridCacheEntryFlags.DisableDistributedCache
            };
            await _hybridCache.SetAsync(key, value!, options, tags: null, cancellationToken: token);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        await _hybridCache.RemoveAsync(key, token);
    }

    private static bool IsRedisOom(RedisServerException ex)
    {
        return ex.Message.Contains("OOM command not allowed", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNamespaceFromKey(string key)
    {
        var index = key.IndexOf(':');
        return index > 0 ? key[..index] : string.Empty;
    }

    private async Task PurgeNamespaceBestEffortAsync(string @namespace)
    {
        // Best-effort: we don't have server-side KEYS access in many managed Redis offerings.
        // Intentionally no-op; hook point for future server-side eviction via tags.
        await Task.CompletedTask;
    }

    private static bool IsTooLargeForDistributed<T>(T value)
    {
        try
        {
            // Try a conservative size estimate
            if (value is string s)
            {
                return System.Text.Encoding.UTF8.GetByteCount(s) > MaxBytesForDistributed;
            }

            if (value is byte[] b)
            {
                return b.Length > MaxBytesForDistributed;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(value);
            return System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytesForDistributed;
        }
        catch
        {
            return false; // if sizing fails, don't block distribute
        }
    }
}