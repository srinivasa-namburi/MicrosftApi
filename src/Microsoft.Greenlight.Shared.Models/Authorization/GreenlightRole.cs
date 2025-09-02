using Orleans;

namespace Microsoft.Greenlight.Shared.Models.Authorization;

/// <summary>
/// Represents a role in Greenlight. May be mapped to an Entra App Role (by Id) or be local-only.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public sealed class GreenlightRole
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique name for the role inside Greenlight (e.g. "Administrators", "Editors").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional: Entra App Role object Id. Used for mapping when available in token's 'xms_roles' claim.
    /// This is less commonly available than EntraAppRoleValue. When both are set, both must match 
    /// for the mapping to be considered valid.
    /// </summary>
    public Guid? EntraAppRoleId { get; set; }

    /// <summary>
    /// Primary: Entra App Role value/name emitted in the 'roles' claim. This is the most common 
    /// way to map Entra App Roles to Greenlight roles. When set, users with this role name 
    /// in their token will be auto-assigned/unassigned to this Greenlight role.
    /// </summary>
    public string? EntraAppRoleValue { get; set; }

    /// <summary>
    /// Whether the role is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Permissions granted to this role.
    /// </summary>
    public ICollection<GreenlightRolePermission> RolePermissions { get; set; } = new List<GreenlightRolePermission>();
}
