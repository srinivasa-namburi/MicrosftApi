// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.McpServer.Flow.Contracts.Responses;

namespace Microsoft.Greenlight.McpServer.Flow.Services;

/// <summary>
/// Service for creating time-limited proxy URLs for content references.
/// These proxy URLs provide anonymous, temporary access to content without exposing internal URLs.
/// </summary>
public interface IContentRelinkerService
{
    /// <summary>
    /// Generates a time-limited proxy URL for a content reference.
    /// </summary>
    /// <param name="originalUrl">The original internal URL or path.</param>
    /// <param name="referenceType">Type of content being referenced.</param>
    /// <param name="expirationMinutes">Minutes until the proxy URL expires (default: 10).</param>
    /// <param name="metadata">Optional metadata to associate with the link.</param>
    /// <returns>A proxy URL that expires after the specified time.</returns>
    Task<string> GenerateProxyUrlAsync(
        string originalUrl,
        string referenceType,
        int expirationMinutes = 10,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Validates and resolves a proxy URL to its original target.
    /// </summary>
    /// <param name="proxyToken">The proxy token from the URL.</param>
    /// <returns>The original URL if valid and not expired; null otherwise.</returns>
    Task<(bool IsValid, string? OriginalUrl, Dictionary<string, string>? Metadata)> ResolveProxyUrlAsync(string proxyToken);

    /// <summary>
    /// Processes a content reference DTO and updates its URL with a proxy URL.
    /// </summary>
    /// <param name="reference">The content reference to process.</param>
    /// <param name="expirationMinutes">Minutes until the proxy URL expires.</param>
    /// <returns>The reference with updated proxy URL.</returns>
    Task<FlowContentReferenceDTO> ProcessReferenceWithProxyAsync(
        FlowContentReferenceDTO reference,
        int expirationMinutes = 10);

    /// <summary>
    /// Processes multiple content references and updates their URLs with proxy URLs.
    /// </summary>
    /// <param name="references">The content references to process.</param>
    /// <param name="expirationMinutes">Minutes until the proxy URLs expire.</param>
    /// <returns>The references with updated proxy URLs.</returns>
    Task<List<FlowContentReferenceDTO>> ProcessReferencesWithProxyAsync(
        List<FlowContentReferenceDTO> references,
        int expirationMinutes = 10);

    /// <summary>
    /// Cleans up expired proxy URL mappings from cache.
    /// </summary>
    /// <returns>Number of expired entries removed.</returns>
    Task<int> CleanupExpiredProxiesAsync();
}