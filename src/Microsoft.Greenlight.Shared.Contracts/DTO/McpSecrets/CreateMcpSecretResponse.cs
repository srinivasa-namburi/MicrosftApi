// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets;

/// <summary>
/// Response after creating an MCP API secret. The plaintext secret value is returned once.
/// </summary>
public sealed class CreateMcpSecretResponse
{
    public McpSecretInfo Secret { get; set; } = new();
    public string Plaintext { get; set; } = string.Empty;
}

