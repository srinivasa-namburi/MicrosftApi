// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Request to create or update a role.
/// </summary>
public sealed class UpsertRoleRequest
{
    /// <summary>
    /// The role identifier; null or empty to create a new role.
    /// </summary>
    public Guid? Id { get; set; }
    
    /// <summary>
    /// The role display name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Optional description for the role.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Optional Entra App Role Id to bind/sync.
    /// </summary>
    public Guid? EntraAppRoleId { get; set; }
    
    /// <summary>
    /// Optional Entra App Role value/name to bind/sync.
    /// </summary>
    public string? EntraAppRoleValue { get; set; }
}