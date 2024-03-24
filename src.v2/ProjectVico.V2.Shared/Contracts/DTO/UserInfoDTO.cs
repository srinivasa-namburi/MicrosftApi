namespace ProjectVico.V2.Shared.Contracts.DTO;

public record UserInfoDTO(string ProviderSubjectId, string FullName)
{
    public string? Email { get; set; }
    public Guid Id { get; set; }
};