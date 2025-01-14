namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents data transfer object for user information
/// </summary>
public record UserInfoDTO(string ProviderSubjectId, string FullName)
{
    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Unique identifier of the user.
    /// </summary>
    public Guid Id { get; set; }
};
