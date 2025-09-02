namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

public sealed class PermissionInfo
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
