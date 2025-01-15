using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents user information with authentication details.
/// </summary>
public class UserInformation : EntityBase
{
    /// <summary>
    /// Full name of the user.
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Provider subject identifier.
    /// </summary>
    public required string ProviderSubjectId { get; set; }

    /// <summary>
    /// Authentication provider used by the user.
    /// </summary>
    public AuthenticationProvider Provider { get; set; } = AuthenticationProvider.AzureAD;

    /// <summary>
    /// Email of the user.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User's theme preference.
    /// </summary>
    public ThemePreference ThemePreference { get; set; } = ThemePreference.System;
}

/// <summary>
/// Represents the authentication provider.
/// </summary>
public enum AuthenticationProvider
{
    /// <summary>
    /// Azure Active Directory.
    /// </summary>
    AzureAD
}
