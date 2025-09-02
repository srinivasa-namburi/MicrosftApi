namespace Microsoft.Greenlight.Shared.Models.Authorization;

/// <summary>
/// Join entity linking a role to a permission.
/// </summary>
public sealed class GreenlightRolePermission
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Role foreign key.
    /// </summary>
    public Guid RoleId { get; set; }
    /// <summary>
    /// Permission foreign key.
    /// </summary>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Navigation to role.
    /// </summary>
    public GreenlightRole? Role { get; set; }
    /// <summary>
    /// Navigation to permission.
    /// </summary>
    public GreenlightPermission? Permission { get; set; }
}
