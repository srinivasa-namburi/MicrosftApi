// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.Greenlight.McpServer.Flow.Models;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.McpServer.Flow.Services;

/// <summary>
/// Manages Greenlight business sessions (separate from MCP SDK protocol sessions).
/// Uses X-Greenlight-Session header for tracking user context and conversation state.
/// </summary>
public class McpSessionManager : IMcpSessionManager
{
    private const string HeaderName = "X-Greenlight-Session";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly IAppCache _cache;
    private readonly ILogger<McpSessionManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpSessionManager"/> class.
    /// </summary>
    public McpSessionManager(IAppCache cache, ILogger<McpSessionManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(string serverNamespace, IHeaderDictionary headers, CancellationToken cancellationToken)
    {
        // Look for X-Greenlight-Session header
        if (!headers.TryGetValue(HeaderName, out var values))
        {
            _logger.LogDebug("ResolvePrincipalFromHeadersAsync: No X-Greenlight-Session header found");
            return null;
        }

        var sessionId = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogDebug("ResolvePrincipalFromHeadersAsync: X-Greenlight-Session header is empty");
            return null;
        }

        _logger.LogDebug("ResolvePrincipalFromHeadersAsync: Looking up session {SessionId} in namespace {ServerNamespace}", sessionId, serverNamespace);

        // Try to get the session
        var session = await GetSessionAsync(serverNamespace, sessionId, cancellationToken);
        if (session == null)
        {
            _logger.LogWarning("ResolvePrincipalFromHeadersAsync: Session {SessionId} not found in cache for namespace {ServerNamespace}", sessionId, serverNamespace);
            return null;
        }

        // Get ProviderSubjectId from session data (stored by middleware)
        var providerSubjectId = await GetSessionDataAsync(serverNamespace, sessionId, "_providerSubjectId", cancellationToken);

        if (string.IsNullOrWhiteSpace(providerSubjectId))
        {
            _logger.LogWarning("ResolvePrincipalFromHeadersAsync: Session {SessionId} has no ProviderSubjectId in session data", sessionId);
            return null;
        }

        _logger.LogDebug("ResolvePrincipalFromHeadersAsync: Successfully resolved session {SessionId} for user {ProviderSubjectId}",
            sessionId, providerSubjectId);

        // Create BOTH "sub" and "oid" claims with the same value for maximum compatibility
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, providerSubjectId),
            new Claim("sub", providerSubjectId), // ProviderSubjectId - primary identifier used throughout system
            new Claim("oid", providerSubjectId)  // Also add oid for backwards compatibility
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "McpSession");
        return new ClaimsPrincipal(identity);
    }

    /// <inheritdoc />
    public async Task<string?> GetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mcpSessionId) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var session = await GetSessionAsync(serverNamespace, mcpSessionId, cancellationToken);
        if (session == null)
        {
            return null;
        }

        // SessionData is guaranteed to be initialized by the property getter
        return session.SessionData.TryGetValue(key, out var value) ? value : null;
    }

    /// <inheritdoc />
    public async Task SetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mcpSessionId) || string.IsNullOrWhiteSpace(key))
        {
            _logger.LogDebug("SetSessionDataAsync called with empty mcpSessionId or key");
            return;
        }

        var cacheKey = GetSessionCacheKey(serverNamespace, mcpSessionId);
        _logger.LogDebug("SetSessionDataAsync: Setting {Key}={Value} for session {McpSessionId} in namespace {ServerNamespace} (cache key: {CacheKey})",
            key, value, mcpSessionId, serverNamespace, cacheKey);

        var session = await _cache.GetOrCreateAsync<McpSession>(
            cacheKey,
            async _ =>
            {
                // Create a new session if it doesn't exist
                var newSession = new McpSession
                {
                    SessionId = mcpSessionId,
                    ProviderSubjectId = string.Empty, // Will be updated when _providerSubjectId key is set
                    CreatedUtc = DateTime.UtcNow,
                    ExpiresUtc = DateTime.UtcNow.Add(Ttl)
                };
                _logger.LogInformation("SetSessionDataAsync: Created new MCP session cache entry for session {McpSessionId}", mcpSessionId);
                return await Task.FromResult(newSession);
            },
            Ttl,
            allowDistributed: true,
            cancellationToken);

        // Handle corrupted/incompatible cached sessions (e.g., from schema changes)
        if (session == null || string.IsNullOrEmpty(session.SessionId))
        {
            _logger.LogWarning("SetSessionDataAsync: Cached session for {McpSessionId} is invalid/corrupted - recreating", mcpSessionId);

            // Remove the corrupted entry
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            // Create a fresh session
            session = new McpSession
            {
                SessionId = mcpSessionId,
                ProviderSubjectId = string.Empty,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.Add(Ttl)
            };

            // Cache the new session
            await _cache.SetAsync(cacheKey, session, Ttl, allowDistributed: true, cancellationToken);
            _logger.LogInformation("SetSessionDataAsync: Recreated and cached session for {McpSessionId}", mcpSessionId);
        }

        // SessionData is guaranteed to be initialized by the property getter
        session.SessionData[key] = value;

        // If setting ProviderSubjectId, also update the main property for consistency
        if (key == "_providerSubjectId")
        {
            session.ProviderSubjectId = value;
            _logger.LogDebug("SetSessionDataAsync: Updated McpSession.ProviderSubjectId to {ProviderSubjectId}", value);
        }

        session.ExpiresUtc = DateTime.UtcNow.Add(Ttl);
        await _cache.SetAsync(cacheKey, session, Ttl, allowDistributed: true, cancellationToken);

        _logger.LogInformation("SetSessionDataAsync: Successfully set {Key}={Value} for MCP session {McpSessionId}, SessionData count: {Count}",
            key, value, mcpSessionId, session.SessionData.Count);
    }

    /// <inheritdoc />
    public async Task RemoveSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mcpSessionId) || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var session = await GetSessionAsync(serverNamespace, mcpSessionId, cancellationToken);
        if (session == null)
        {
            return;
        }

        // SessionData is guaranteed to be initialized by the property getter
        if (session.SessionData.Remove(key))
        {
            var cacheKey = GetSessionCacheKey(serverNamespace, mcpSessionId);
            session.ExpiresUtc = DateTime.UtcNow.Add(Ttl);
            await _cache.SetAsync(cacheKey, session, Ttl, allowDistributed: true, cancellationToken);
            _logger.LogDebug("Removed session data {Key} for MCP session {McpSessionId} in namespace {ServerNamespace}", key, mcpSessionId, serverNamespace);
        }
    }

    /// <inheritdoc />
    public async Task<McpSession?> GetSessionAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mcpSessionId))
        {
            _logger.LogDebug("GetSessionAsync called with null/empty mcpSessionId");
            return null;
        }

        var cacheKey = GetSessionCacheKey(serverNamespace, mcpSessionId);
        _logger.LogDebug("GetSessionAsync: Looking up session with key {CacheKey}", cacheKey);

        var session = await _cache.GetOrCreateAsync<McpSession>(
            cacheKey,
            async _ =>
            {
                _logger.LogWarning("GetSessionAsync: Session {McpSessionId} not found in cache for namespace {ServerNamespace} - returning null",
                    mcpSessionId, serverNamespace);
                return await Task.FromResult<McpSession>(null!);
            },
            Ttl,
            allowDistributed: true,
            cancellationToken);

        var isValid = session is not null && !string.IsNullOrEmpty(session.SessionId);
        _logger.LogDebug("GetSessionAsync: Session {McpSessionId} in namespace {ServerNamespace} - Found: {Found}, Valid: {Valid}, SessionId: {SessionId}, ProviderSubjectId: {ProviderSubjectId}",
            mcpSessionId, serverNamespace, session is not null, isValid, session?.SessionId ?? "null", session?.ProviderSubjectId ?? "null");

        return isValid ? session : null;
    }

    /// <inheritdoc />
    public Task<string?> GetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken)
    {
        return GetSessionDataAsync(serverNamespace, mcpSessionId, "_providerSubjectId", cancellationToken);
    }

    /// <inheritdoc />
    public Task SetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, string providerSubjectId, CancellationToken cancellationToken)
    {
        return SetSessionDataAsync(serverNamespace, mcpSessionId, "_providerSubjectId", providerSubjectId, cancellationToken);
    }

    /// <inheritdoc />
    public string? ResolveProviderSubjectId(ClaimsPrincipal user)
    {
        if (user == null)
        {
            return null;
        }

        // Canonical order: "sub" → ClaimTypes.NameIdentifier → "oid"
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("oid")?.Value;
    }

    /// <summary>
    /// Builds a namespaced session cache key for Greenlight business sessions.
    /// </summary>
    private static string GetSessionCacheKey(string serverNamespace, string sessionId)
    {
        return $"greenlight:{serverNamespace}:sessions:{sessionId}";
    }
}
