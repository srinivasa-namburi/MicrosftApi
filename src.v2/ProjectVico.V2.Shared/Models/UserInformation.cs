namespace ProjectVico.V2.Shared.Models;

public class UserInformation : EntityBase
{
    public string FullName { get; set; } 
    public string ProviderSubjectId { get; set; }
    public AuthenticationProvider Provider { get; set; } = AuthenticationProvider.AzureAD;
    public string? Email { get; set; } 

}

public enum AuthenticationProvider
{
    AzureAD
}