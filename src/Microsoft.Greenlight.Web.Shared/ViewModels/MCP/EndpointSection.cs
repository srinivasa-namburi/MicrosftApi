// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// Endpoint-specific options (Core/Flow) where applicable.
/// </summary>
public sealed class EndpointSection
{
    public bool SecretEnabled { get; set; }
    public string? SecretHeaderName { get; set; }
}

