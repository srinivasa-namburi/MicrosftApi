// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Request to assign a role to a user.
/// </summary>
public sealed class AssignUserRoleRequest
{
    /// <summary>
    /// The user's provider subject identifier.
    /// </summary>
    public required string ProviderSubjectId { get; set; }
    
    /// <summary>
    /// The role identifier.
    /// </summary>
    public Guid RoleId { get; set; }
    
    /// <summary>
    /// Whether the assignment originates from Entra App Role membership.
    /// </summary>
    public bool IsFromEntra { get; set; }
}