// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Greenlight.McpServer.Models;

namespace Microsoft.Greenlight.McpServer.Services;

/// <summary>
/// Manages MCP sessions and mappings between MCP session IDs and Flow conversation IDs.
/// </summary>
public interface IMcpSessionManager
{
    /// <summary>
    /// Creates a new session for the specified user and persists it with a sliding TTL.
    /// </summary>
    /// <param name="user">The authenticated user principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created <see cref="McpSession"/>.</returns>
    Task<McpSession> CreateAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes the session TTL and returns the updated session if present.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple indicating success and the updated session when found.</returns>
    Task<(bool Ok, McpSession? Session)> RefreshAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates the session and removes it from cache.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to resolve a <see cref="ClaimsPrincipal"/> from the X-MCP-Session header.
    /// </summary>
    /// <param name="headers">Request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The principal when found; otherwise null.</returns>
    Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(IHeaderDictionary headers, CancellationToken cancellationToken);

    /// <summary>
    /// Lists active sessions known to the cache for admin purposes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of active sessions.</returns>
    Task<List<McpSession>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates a Flow conversation ID for the given MCP session ID.
    /// Maintains a mapping between MCP session IDs (from clients) and Flow conversation GUIDs.
    /// </summary>
    /// <param name="mcpSessionId">The MCP session ID from the client.</param>
    /// <param name="user">The user principal if available.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Flow conversation GUID, or null if unable to map.</returns>
    Task<Guid?> GetOrCreateFlowSessionAsync(string mcpSessionId, ClaimsPrincipal? user, CancellationToken cancellationToken);
}
