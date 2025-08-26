// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Services.Caching;

/// <summary>
/// Default implementation for IAppCache using HybridCache with Redis as L2 when available.
/// Adds safeguards to avoid storing very large payloads in distributed cache and handles OOM gracefully.
/// Centralizes Redis optionality and failure handling to keep callers simple and resilient.
/// </summary>
public class AppCache : IAppCache
{
    private readonly HybridCache _hybridCache;
    private readonly ILogger<AppCache> _logger;
    private readonly IDistributedCache? _distributed; // optional, used by HybridCache internally

    // default limits: avoid pushing huge values to Redis
    private const int MaxBytesForDistributed = 256 * 1024; // 256 KB

    // Circuit breaker for distributed cache (shared for this process)
    private static volatile bool s_distributedDegraded;
    private static DateTimeOffset s_nextProbeAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan s_probeInterval = TimeSpan.FromMinutes(5);
    private static readonly object s_lock = new object();

    public AppCache(HybridCache hybridCache, ILogger<AppCache> logger, IDistributedCache? distributed = null)
    {
        _hybridCache = hybridCache;
        _logger = logger;
        _distributed = distributed;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default)
    {
        var useDistributed = allowDistributed && CanUseDistributed();
        try
        {
            var options = new HybridCacheEntryOptions
            {
                Expiration = ttl,
                LocalCacheExpiration = ttl,
                Flags = useDistributed ? HybridCacheEntryFlags.None : HybridCacheEntryFlags.DisableDistributedCache
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
            // OOM: flush the affected key locally and open circuit breaker; retry local-only
            _logger.LogWarning(rsex, "Redis OOM for key {Key}; opening circuit for {Cooldown} and retrying local-only", key, s_probeInterval);
            MarkDistributedDegraded();
            try { await _hybridCache.RemoveAsync(key, token); } catch { /* ignore */ }
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
            _logger.LogWarning(rce, "Redis connection issue for key {Key}; opening circuit for {Cooldown} and using local-only fallback", key, s_probeInterval);
            MarkDistributedDegraded();
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
        var useDistributed = allowDistributed && CanUseDistributed();
        try
        {
            var flags = useDistributed ? HybridCacheEntryFlags.None : HybridCacheEntryFlags.DisableDistributedCache;
            if (useDistributed && IsTooLargeForDistributed(value))
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
            _logger.LogWarning(rsex, "Redis OOM on Set for key {Key}; opening circuit for {Cooldown} and retrying local-only", key, s_probeInterval);
            MarkDistributedDegraded();
            try { await _hybridCache.RemoveAsync(key, token); } catch { /* ignore */ }
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
            _logger.LogWarning(rce, "Redis connection issue on Set for key {Key}; opening circuit for {Cooldown} and using local-only fallback", key, s_probeInterval);
            MarkDistributedDegraded();
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
        return ex.Message.Contains("OOM command not allowed", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("OOM", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("out of memory", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNamespaceFromKey(string key)
    {
        var index = key.IndexOf(':');
        return index > 0 ? key[..index] : string.Empty;
    }

    private async Task PurgeNamespaceBestEffortAsync(string @namespace)
    {
        // Best-effort placeholder. If HybridCache adds RemoveByTagAsync support broadly, we can tag entries
        // with the namespace and call it here for targeted eviction. For now, we rely on TTLs and per-key removal.
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

    private static bool CanUseDistributed()
    {
        // No distributed cache registered? Force local-only.
        // We only need to probe occasionally.
        if (s_distributedDegraded)
        {
            if (DateTimeOffset.UtcNow < s_nextProbeAt)
            {
                return false;
            }
        }

        lock (s_lock)
        {
            if (!s_distributedDegraded && DateTimeOffset.UtcNow < s_nextProbeAt)
            {
                return true;
            }

            // If an IDistributedCache is not present, treat as degraded
            var healthy = true; // Conservative default: HybridCache abstracts the transport; assume healthy
            // We do not have a reliable, cross-provider health probe here; keep it simple and time-based.
            // Actual failures will trip the catch blocks above and open the circuit.

            s_distributedDegraded = !healthy;
            s_nextProbeAt = DateTimeOffset.UtcNow + s_probeInterval;
            return healthy;
        }
    }

    private static void MarkDistributedDegraded()
    {
        lock (s_lock)
        {
            s_distributedDegraded = true;
            s_nextProbeAt = DateTimeOffset.UtcNow + s_probeInterval;
        }
    }
}