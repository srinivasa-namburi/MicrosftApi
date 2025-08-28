using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Helper class to obtain Azure credentials.
/// </summary>
public class AzureCredentialHelper
{
    private readonly IConfiguration _configuration;
    private Uri? _authorityHost;

    /// <summary>
    /// Authority Host used by this instance of the solution.
    /// Only works after Initialize() has been called.
    /// </summary>
    public string DiscoveredAuthorityHost => _authorityHost?.ToString() ?? string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureCredentialHelper"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    public AzureCredentialHelper(IConfiguration configuration)
    {
        _configuration = configuration;
        Initialize();
    }

    /// <summary>
    /// Gets the Azure credential based on the configuration.
    /// </summary>
    /// <returns>A <see cref="TokenCredential"/> instance.</returns>
    public TokenCredential GetAzureCredential()
    {
        TokenCredential? credential;
        // If there is no specific tenant ID, use the default Azure credential
        if (string.IsNullOrEmpty(_configuration["Azure:TenantId"]))
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = _authorityHost,
                AdditionallyAllowedTenants = { "*" }
            });

        }
        else
        {
            var credentialSource = _configuration["Azure:CredentialSource"];
            if (credentialSource == "AzureCli")
            {
                credential = new AzureCliCredential(new AzureCliCredentialOptions()
                {
                    AuthorityHost = _authorityHost,
                    TenantId = _configuration["Azure:TenantId"]
                });

            }
            else
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    AuthorityHost = _authorityHost,
                    TenantId = _configuration["Azure:TenantId"]
                });
            }
        }

        return credential;
    }

    private void Initialize()
    {
        var azureInstance = _configuration["AzureAd:Instance"];
        if (!string.IsNullOrEmpty(azureInstance) &&
            azureInstance.Contains(AzureAuthorityHosts.AzureGovernment.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _authorityHost = AzureAuthorityHosts.AzureGovernment;
        }
        else
        {
            _authorityHost = AzureAuthorityHosts.AzurePublicCloud;
        }
    }

}
