// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets;

/// <summary>
/// Request to create a new MCP API secret.
/// </summary>
public sealed class CreateMcpSecretRequest
{
    public string Name { get; set; } = string.Empty;
    public string UserOid { get; set; } = string.Empty;
}

