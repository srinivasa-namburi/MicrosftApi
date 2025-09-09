// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Request to assign or change the mapping from an Entra App Role to a specific internal role.
/// </summary>
public sealed class UpsertEntraRoleMappingRequest
{
    /// <summary>
    /// The Entra App Role value/name (preferred). Required if <see cref="EntraAppRoleId"/> is not supplied.
    /// </summary>
    public string? EntraAppRoleValue { get; set; }

    /// <summary>
    /// Optional Entra App Role object ID. Used when role IDs are present and stable. Required if <see cref="EntraAppRoleValue"/> is not supplied.
    /// </summary>
    public Guid? EntraAppRoleId { get; set; }

    /// <summary>
    /// The target internal role to map the Entra role to.
    /// </summary>
    public required Guid TargetRoleId { get; set; }
}

