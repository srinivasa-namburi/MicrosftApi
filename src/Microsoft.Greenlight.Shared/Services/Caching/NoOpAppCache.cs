// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Services.Caching;

/// <summary>
/// No-op implementation used as a fallback if a real cache isn't available.
/// </summary>
public sealed class NoOpAppCache : IAppCache
{
    public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default)
        => factory(token);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, bool allowDistributed = true, CancellationToken token = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken token = default)
        => Task.CompletedTask;
}