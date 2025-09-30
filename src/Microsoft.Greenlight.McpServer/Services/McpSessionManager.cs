// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.McpServer.Models;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.McpServer.Services;

/// <summary>
/// Manages MCP sessions and mappings between MCP session IDs and Flow conversation IDs.
/// </summary>
public class McpSessionManager : IMcpSessionManager
{
    private const string HeaderName = "X-MCP-Session";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private const string IndexKey = "mcp:sessions:index";

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
    public async Task<McpSession> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var oid = user.FindFirst("oid")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? throw new InvalidOperationException("Cannot create session without a user object id claim.");

        var session = new McpSession
        {
            SessionId = Guid.NewGuid(),
            UserObjectId = oid,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(Ttl)
        };

        await _cache.SetAsync(GetKey(session.SessionId), session, Ttl, allowDistributed: true, cancellationToken);
        // maintain index
        var index = await LoadIndexAsync(cancellationToken);
        if (index.Add(session.SessionId))
        {
            await SaveIndexAsync(index, cancellationToken);
        }
        _logger.LogInformation("Created MCP session {SessionId} for {User}", session.SessionId, oid);
        return session;
    }

    /// <inheritdoc />
    public async Task<(bool Ok, McpSession? Session)> RefreshAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var key = GetKey(sessionId);
        var session = await _cache.GetOrCreateAsync<McpSession>(
            key,
            async _ => await Task.FromResult<McpSession>(null!),
            Ttl,
            allowDistributed: true,
            cancellationToken);

        if (session is null || session.SessionId == Guid.Empty)
        {
            return (false, null);
        }

        session.ExpiresUtc = DateTime.UtcNow.Add(Ttl);
        await _cache.SetAsync(key, session, Ttl, allowDistributed: true, cancellationToken);
        _logger.LogDebug("Refreshed MCP session {SessionId}", sessionId);
        return (true, session);
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(GetKey(sessionId), cancellationToken);
        var index = await LoadIndexAsync(cancellationToken);
        if (index.Remove(sessionId))
        {
            await SaveIndexAsync(index, cancellationToken);
        }
        _logger.LogInformation("Invalidated MCP session {SessionId}", sessionId);
    }

    /// <inheritdoc />
    public async Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(IHeaderDictionary headers, CancellationToken cancellationToken)
    {
        if (!headers.TryGetValue(HeaderName, out var values))
        {
            return null;
        }

        var sessionRaw = values.FirstOrDefault();
        if (!Guid.TryParse(sessionRaw, out var sessionId))
        {
            return null;
        }

        var session = await _cache.GetOrCreateAsync<McpSession>(
            GetKey(sessionId),
            async _ => await Task.FromResult<McpSession>(null!),
            Ttl,
            allowDistributed: true,
            cancellationToken);

        if (session is null || string.IsNullOrWhiteSpace(session.UserObjectId))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserObjectId),
            new Claim("oid", session.UserObjectId)
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "McpSession");
        return new ClaimsPrincipal(identity);
    }

    /// <inheritdoc />
    public async Task<List<McpSession>> ListAsync(CancellationToken cancellationToken)
    {
        var index = await LoadIndexAsync(cancellationToken);
        var sessions = new List<McpSession>();
        var removed = false;
        foreach (var id in index.ToArray())
        {
            var s = await _cache.GetOrCreateAsync<McpSession>(
                GetKey(id),
                async _ => await Task.FromResult<McpSession>(null!),
                Ttl,
                allowDistributed: true,
                cancellationToken);
            if (s is null || s.SessionId == Guid.Empty)
            {
                index.Remove(id);
                removed = true;
                continue;
            }
            sessions.Add(s);
        }
        if (removed)
        {
            await SaveIndexAsync(index, cancellationToken);
        }
        return sessions.OrderBy(s => s.ExpiresUtc).ToList();
    }

    private static string GetKey(Guid id)
    {
        return $"mcp:sessions:{id}";
    }

    private async Task<HashSet<Guid>> LoadIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var set = await _cache.GetOrCreateAsync<HashSet<Guid>>(
                IndexKey,
                _ => Task.FromResult(new HashSet<Guid>()),
                // Keep index reasonably long-lived; it is maintained on create/invalidate
                TimeSpan.FromHours(12),
                allowDistributed: true,
                cancellationToken);
            return set ?? new HashSet<Guid>();
        }
        catch
        {
            return new HashSet<Guid>();
        }
    }

    private Task SaveIndexAsync(HashSet<Guid> index, CancellationToken cancellationToken)
    {
        return _cache.SetAsync(IndexKey, index, TimeSpan.FromHours(12), allowDistributed: true, cancellationToken);
    }


    /// <inheritdoc />
    public async Task<Guid?> GetOrCreateFlowSessionAsync(string mcpSessionId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(mcpSessionId))
        {
            return null;
        }

        // Key for mapping MCP session ID to Flow conversation ID
        var mappingKey = $"mcp:flow-mapping:{mcpSessionId}";

        // Try to get existing mapping
        var existingFlowId = await _cache.GetOrCreateAsync<Guid?>(
            mappingKey,
            async _ =>
            {
                // No existing mapping - create new Flow conversation ID
                var flowId = Guid.NewGuid();
                _logger.LogInformation("Created new Flow conversation {FlowId} for MCP session {McpSessionId}", flowId, mcpSessionId);
                return flowId;
            },
            Ttl,
            allowDistributed: true,
            cancellationToken);

        if (existingFlowId.HasValue && existingFlowId.Value != Guid.Empty)
        {
            _logger.LogDebug("Using existing Flow conversation {FlowId} for MCP session {McpSessionId}", existingFlowId.Value, mcpSessionId);

            // Refresh the TTL on the mapping
            await _cache.SetAsync(mappingKey, existingFlowId.Value, Ttl, allowDistributed: true, cancellationToken);
        }

        return existingFlowId;
    }

}
