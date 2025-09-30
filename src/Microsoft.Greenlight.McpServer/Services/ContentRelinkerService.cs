// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Greenlight.McpServer.Contracts.Responses;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.McpServer.Services;

/// <summary>
/// Implementation of content relinker service for time-limited proxy URLs.
/// </summary>
public class ContentRelinkerService : IContentRelinkerService
{
    private readonly IAppCache _cache;
    private readonly ILogger<ContentRelinkerService> _logger;
    private const string CACHE_KEY_PREFIX = "relink:";

    public ContentRelinkerService(
        IAppCache cache,
        ILogger<ContentRelinkerService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateProxyUrlAsync(
        string originalUrl,
        string referenceType,
        int expirationMinutes = 10,
        Dictionary<string, string>? metadata = null)
    {
        try
        {
            // Generate a unique token for this proxy URL
            var token = GenerateSecureToken();

            // Create proxy data
            var proxyData = new ProxyUrlData
            {
                OriginalUrl = originalUrl,
                ReferenceType = referenceType,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            // Store in cache with expiration
            var cacheKey = $"{CACHE_KEY_PREFIX}{token}";
            var serializedData = JsonSerializer.Serialize(proxyData);
            await _cache.SetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(expirationMinutes));

            // Return the proxy URL path (will be combined with base URL in controller)
            return $"/api/content/proxy/{token}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating proxy URL for {OriginalUrl}", originalUrl);
            // Return original URL as fallback
            return originalUrl;
        }
    }

    /// <inheritdoc/>
    public async Task<(bool IsValid, string? OriginalUrl, Dictionary<string, string>? Metadata)> ResolveProxyUrlAsync(string proxyToken)
    {
        try
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{proxyToken}";

            // Try to get from cache - returns null if not found
            var serializedData = await _cache.GetOrCreateAsync<string?>(cacheKey,
                async _ => await Task.FromResult<string?>(null),
                TimeSpan.FromSeconds(1), // Very short TTL for lookup
                allowDistributed: true);

            if (string.IsNullOrEmpty(serializedData))
            {
                return (false, null, null);
            }

            var proxyData = JsonSerializer.Deserialize<ProxyUrlData>(serializedData);
            if (proxyData == null || proxyData.ExpiresUtc < DateTime.UtcNow)
            {
                return (false, null, null);
            }

            return (true, proxyData.OriginalUrl, proxyData.Metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving proxy token {Token}", proxyToken);
            return (false, null, null);
        }
    }

    /// <inheritdoc/>
    public async Task<FlowContentReferenceDTO> ProcessReferenceWithProxyAsync(
        FlowContentReferenceDTO reference,
        int expirationMinutes = 10)
    {
        if (string.IsNullOrEmpty(reference.Url))
        {
            return reference;
        }

        try
        {
            var proxyUrl = await GenerateProxyUrlAsync(
                reference.Url,
                reference.ReferenceType,
                expirationMinutes,
                reference.Metadata);

            return reference with
            {
                Url = proxyUrl,
                Metadata = reference.Metadata.Concat(new[]
                {
                    new KeyValuePair<string, string>("proxyExpiration", DateTime.UtcNow.AddMinutes(expirationMinutes).ToString("O")),
                    new KeyValuePair<string, string>("originalUrlHidden", "true")
                }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reference with proxy for {ReferenceId}", reference.Id);
            return reference;
        }
    }

    /// <inheritdoc/>
    public async Task<List<FlowContentReferenceDTO>> ProcessReferencesWithProxyAsync(
        List<FlowContentReferenceDTO> references,
        int expirationMinutes = 10)
    {
        var tasks = references.Select(r => ProcessReferenceWithProxyAsync(r, expirationMinutes));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <inheritdoc/>
    public async Task<int> CleanupExpiredProxiesAsync()
    {
        // This would typically be handled by cache expiration
        // but we can implement manual cleanup if needed
        _logger.LogDebug("Proxy URL cleanup triggered - handled by cache TTL");
        return await Task.FromResult(0);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private class ProxyUrlData
    {
        public string OriginalUrl { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}