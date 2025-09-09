// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Represents a mapping from a Microsoft Entra App Role to an internal Greenlight role.
/// </summary>
public sealed class EntraRoleMappingInfo
{
    /// <summary>
    /// The Entra App Role value/name as emitted in the token 'roles' claim.
    /// </summary>
    public string? EntraAppRoleValue { get; set; }

    /// <summary>
    /// Optional Entra App Role object ID as emitted in 'xms_roles'.
    /// </summary>
    public Guid? EntraAppRoleId { get; set; }

    /// <summary>
    /// The mapped internal role ID, if any.
    /// </summary>
    public Guid? MappedRoleId { get; set; }

    /// <summary>
    /// The mapped internal role name, if any.
    /// </summary>
    public string? MappedRoleName { get; set; }
}

