namespace Microsoft.Greenlight.Shared.Configuration;

/// <summary>
/// Configuration options for Microsoft Entra (previously known as Azure Active Directory).
/// </summary>
public class AzureAdOptions
{
    /// <summary>
    /// Microsoft Entra Instance.
    /// </summary>
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra Domain.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra Tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra App Registration Client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Entra App Registration Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Callback path for Microsoft Entra App Registration.
    /// </summary>
    public string CallbackPath { get; set; } = string.Empty;

    /// <summary>
    /// Scopes for the Microsoft Entra App Registration.
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of trusted caller Client IDs (application IDs) allowed to invoke protected API flows.
    /// When not set, deployment fills this with the current application's ClientId by default.
    /// </summary>
    public List<string> TrustedCallerClientIds { get; set; } = new();
}
