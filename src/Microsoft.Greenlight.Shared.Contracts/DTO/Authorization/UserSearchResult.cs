namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

public sealed class UserSearchResult
{
    public required string ProviderSubjectId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public List<UserSearchRoleAssignment> Assignments { get; set; } = new();
}

public sealed class UserSearchRoleAssignment
{
    public Guid RoleId { get; set; }
    public required string RoleName { get; set; }
    public bool IsFromEntra { get; set; }
}
