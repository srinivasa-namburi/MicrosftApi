// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.McpServer.Core.Tools;

/// <summary>
/// Marker interface to identify MCP tools that should be available on the Flow endpoint (/flow/mcp).
/// This ensures clean separation between business task tools (/mcp) and conversational AI tools (/flow/mcp).
/// </summary>
public interface IFlowMcpTool
{
    // Marker interface - no members required
}