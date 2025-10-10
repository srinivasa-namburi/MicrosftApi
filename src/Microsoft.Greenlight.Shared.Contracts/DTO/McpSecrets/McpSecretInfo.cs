// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Contracts.DTO.McpSecrets;

/// <summary>
/// Public information about an MCP API secret (no plaintext value).
/// </summary>
public sealed class McpSecretInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderSubjectId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
}

