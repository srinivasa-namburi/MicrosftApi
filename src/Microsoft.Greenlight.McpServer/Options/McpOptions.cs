// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.McpServer.Options;

/// <summary>
/// Options for configuring the MCP server security and behavior.
/// Binds to configuration section: ServiceConfiguration:Mcp
/// </summary>
public sealed class McpOptions
{
    /// <summary>
    /// When true, disables authentication requirements for MCP routes.
    /// Use only for local diagnostics. Bound from 'ServiceConfiguration:Mcp:DisableAuth'.
    /// </summary>
    public bool DisableAuth { get; set; }

    /// <summary>
    /// When true, enables secret-based access using a configured header and secret value.
    /// Bound from 'ServiceConfiguration:Mcp:SecretEnabled'.
    /// </summary>
    public bool SecretEnabled { get; set; }

    /// <summary>
    /// The HTTP header name carrying the secret. Defaults to 'X-MCP-Secret' when empty.
    /// Bound from 'ServiceConfiguration:Mcp:SecretHeaderName'.
    /// </summary>
    public string? SecretHeaderName { get; set; }

    /// <summary>
    /// The expected secret value. Bound from 'ServiceConfiguration:Mcp:SecretValue'.
    /// </summary>
    public string? SecretValue { get; set; }
}

