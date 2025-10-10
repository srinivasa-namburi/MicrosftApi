// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Greenlight.McpServer.Core.Models;

namespace Microsoft.Greenlight.McpServer.Core.Services;

/// <summary>
/// Manages MCP session data storage and user principal resolution.
/// MCP protocol handles session lifecycle - this service provides application data storage keyed by MCP session IDs.
/// </summary>
public interface IMcpSessionManager
{
    /// <summary>
    /// Attempts to resolve a <see cref="ClaimsPrincipal"/> from the X-MCP-Session header.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="headers">Request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The principal when found; otherwise null.</returns>
    Task<ClaimsPrincipal?> ResolvePrincipalFromHeadersAsync(string serverNamespace, IHeaderDictionary headers, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a session data value for the given key.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID from the request headers.</param>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value if found, otherwise null.</returns>
    Task<string?> GetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a session data value for the given key.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID from the request headers.</param>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to store (as string - serialize complex objects to JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetSessionDataAsync(string serverNamespace, string mcpSessionId, string key, string value, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a session data value for the given key.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID from the request headers.</param>
    /// <param name="key">The key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveSessionDataAsync(string serverNamespace, string mcpSessionId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the entire session object for the given MCP session ID.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID from the request headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session if found, otherwise null.</returns>
    Task<McpSession?> GetSessionAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the ProviderSubjectId for the given MCP session.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ProviderSubjectId if found, otherwise null.</returns>
    Task<string?> GetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the ProviderSubjectId for the given MCP session.
    /// </summary>
    /// <param name="serverNamespace">The MCP server namespace (e.g., "business", "flow").</param>
    /// <param name="mcpSessionId">The MCP session ID.</param>
    /// <param name="providerSubjectId">The ProviderSubjectId to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetProviderSubjectIdAsync(string serverNamespace, string mcpSessionId, string providerSubjectId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the ProviderSubjectId from a ClaimsPrincipal using canonical claim order.
    /// Canonical order: "sub" → ClaimTypes.NameIdentifier → "oid".
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The ProviderSubjectId if found, otherwise null.</returns>
    string? ResolveProviderSubjectId(ClaimsPrincipal user);
}
