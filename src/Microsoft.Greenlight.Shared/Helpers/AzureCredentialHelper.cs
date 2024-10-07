using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Shared.Helpers;

public class AzureCredentialHelper
{
    private readonly IConfiguration _configuration;

    public AzureCredentialHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DefaultAzureCredential GetAzureCredential()
    {
        DefaultAzureCredential? credential;
        // If there is no specific tenant ID, use the default Azure credential
        if (string.IsNullOrEmpty(_configuration["Azure:TenantId"]))
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AdditionallyAllowedTenants = { "*" }
            });
        }
        else
        {
            credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = _configuration["Azure:TenantId"]
            });
        }

        return credential;
    }
}
