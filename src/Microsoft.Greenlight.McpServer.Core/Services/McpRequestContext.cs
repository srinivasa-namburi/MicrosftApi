// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;

namespace Microsoft.Greenlight.McpServer.Core.Services;

/// <summary>
/// Scoped service that holds MCP request context information for the current request.
/// Populated by middleware and consumed by MCP tools to avoid infrastructure leakage.
/// </summary>
public class McpRequestContext
{
    /// <summary>
    /// Gets or sets the configured server namespace for this MCP server instance.
    /// Set during server startup (e.g., "business" or "flow").
    /// </summary>
    public static string? ConfiguredServerNamespace { get; set; }

    /// <summary>
    /// Gets or sets the provider subject ID (stable user identifier from "sub" claim).
    /// </summary>
    public string? ProviderSubjectId { get; set; }

    /// <summary>
    /// Gets or sets the Greenlight business session ID (X-Greenlight-Session header).
    /// This is separate from the MCP SDK protocol session (Mcp-Session-Id).
    /// </summary>
    public string? GreenlightSessionId { get; set; }

    /// <summary>
    /// Gets or sets the MCP server namespace identifier (e.g., "business", "flow").
    /// Determines which session namespace to use for session operations.
    /// </summary>
    public string? ServerNamespace { get; set; }

    /// <summary>
    /// Gets or sets the Flow conversation ID associated with this MCP session.
    /// </summary>
    public string? FlowConversationId { get; set; }

    /// <summary>
    /// Gets or sets the claims principal for the current user.
    /// </summary>
    public ClaimsPrincipal? User { get; set; }
}
