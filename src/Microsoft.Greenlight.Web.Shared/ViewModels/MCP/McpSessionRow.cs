// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Web.Shared.ViewModels.MCP;

/// <summary>
/// Lightweight session row for admin listing.
/// </summary>
public sealed class McpSessionRow
{
    public Guid SessionId { get; set; }
    public DateTime ExpiresUtc { get; set; }
}

