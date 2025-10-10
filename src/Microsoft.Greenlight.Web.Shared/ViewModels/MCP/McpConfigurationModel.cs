// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// View model for MCP admin configuration.
/// Simplified to only include global settings (no per-endpoint config).
/// </summary>
public sealed class McpConfigurationModel
{
    /// <summary>
    /// Gets or sets the global MCP configuration settings.
    /// </summary>
    public CommonSection Common { get; set; } = new();
}

