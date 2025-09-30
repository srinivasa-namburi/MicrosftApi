// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// Common MCP options applying to all endpoints.
/// </summary>
public sealed class CommonSection
{
    public bool DisableAuth { get; set; }
    public bool SecretEnabled { get; set; }
    public string? SecretHeaderName { get; set; }
    public string? SecretValue { get; set; }
}

