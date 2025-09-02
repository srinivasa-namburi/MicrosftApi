namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

public sealed class UserRoleAssignmentInfo
{
    public Guid Id { get; set; }
    public required string ProviderSubjectId { get; set; }
    public required Guid RoleId { get; set; }
    public bool IsFromEntra { get; set; }
}
