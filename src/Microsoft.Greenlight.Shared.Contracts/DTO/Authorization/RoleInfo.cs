namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

public sealed class RoleInfo
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? EntraAppRoleId { get; set; }
    public string? EntraAppRoleValue { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> PermissionIds { get; set; } = [];
}
