// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// MCP configuration options.
/// </summary>
public sealed class CommonSection
{
    /// <summary>
    /// Gets or sets a value indicating whether per-user JWT authentication is disabled.
    /// When true, only API secrets (if enabled) are required.
    /// </summary>
    public bool DisableAuth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether API secret authentication is enabled.
    /// Secrets use the fixed header name "X-MCP-Secret".
    /// </summary>
    public bool SecretEnabled { get; set; }
}

