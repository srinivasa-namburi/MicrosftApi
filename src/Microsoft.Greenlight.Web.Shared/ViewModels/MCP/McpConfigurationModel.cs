// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// View model for MCP admin configuration.
/// </summary>
public sealed class McpConfigurationModel
{
    public CommonSection Common { get; set; } = new();
    public EndpointSection Core { get; set; } = new();
    public EndpointSection Flow { get; set; } = new();
}

