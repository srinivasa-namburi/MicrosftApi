// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Plugins;

/// <summary>
/// Lightweight DTO for MCP plugin associations, containing only the necessary information for the UI
/// </summary>
public class McpPluginAssociationInfo
{
    /// <summary>
    /// Gets or sets the plugin ID
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// Gets or sets the document process ID
    /// </summary>
    public Guid DocumentProcessId { get; set; }

    /// <summary>
    /// Gets or sets the association ID
    /// </summary>
    public Guid AssociationId { get; set; }

    /// <summary>
    /// Gets or sets the name (plugin name or document process name depending on context)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to keep on latest version
    /// </summary>
    public bool KeepOnLatestVersion { get; set; }

    /// <summary>
    /// Gets or sets the current version information
    /// </summary>
    public McpPluginVersionInfo? CurrentVersion { get; set; }

    /// <summary>
    /// Gets or sets the available versions
    /// </summary>
    public List<McpPluginVersionInfo> AvailableVersions { get; set; } = new();
}