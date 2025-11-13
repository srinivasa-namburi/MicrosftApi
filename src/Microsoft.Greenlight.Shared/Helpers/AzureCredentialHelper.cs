using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Shared.Helpers
{
    /// <summary>
    /// Helper class to obtain Azure credentials with console logging.
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
        /// Includes console logging for debugging.
        /// </summary>
        /// <returns>A <see cref="TokenCredential"/> instance.</returns>
        public TokenCredential GetAzureCredential()
        {
            Console.WriteLine("🔵 [AzureCredentialHelper] Rajesh Starting credential creation...");

            string? tenantId = _configuration["Azure:TenantId"];
            string? credentialSource = _configuration["Azure:CredentialSource"];
            string? managedIdentityClientId = _configuration["Azure:ManagedIdentityClientId"];

            Console.WriteLine($"🧩 Rajesh TenantId: {tenantId ?? "(none)"}");
            Console.WriteLine($"⚙️  Rajesh CredentialSource: {credentialSource ?? "Default"}");
            Console.WriteLine($"🪪 ManagedIdentityClientId Rajesh: {managedIdentityClientId ?? "(none)"}");
            Console.WriteLine($"🌐 AuthorityHost Rajesh: {_authorityHost}");

            TokenCredential? credential;

            try
            {
                if (string.IsNullOrEmpty(tenantId))
                {
                    Console.WriteLine("➡️ Using DefaultAzureCredential Rajesh (no explicit TenantId)...");
                    credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        AuthorityHost = _authorityHost,
                        AdditionallyAllowedTenants = { "*" }
                    });
                }
                else if (credentialSource == "AzureCli")
                {
                    Console.WriteLine("➡️ Using AzureCliCredential...");
                    credential = new AzureCliCredential(new AzureCliCredentialOptions
                    {
                        AuthorityHost = _authorityHost,
                        TenantId = tenantId
                    });
                }
                else
                {
                    Console.WriteLine("➡️ Using DefaultAzureCredential Rajesh with TenantId and optional ManagedIdentityClientId...");
                    credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        AuthorityHost = _authorityHost,
                        TenantId = tenantId,
                        ManagedIdentityClientId = managedIdentityClientId
                    });
                }

                Console.WriteLine("✅ [AzureCredentialHelper] RajeshCredential created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ [AzureCredentialHelper] rajeshFailed to create Azure credential.");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }

            return credential!;
        }

        /// <summary>
        /// Determines the correct Azure authority host based on configuration.
        /// </summary>
        private void Initialize()
        {
            Console.WriteLine("🧭 [AzureCredentialHelper] Rajesh Initializing Authority Host...");

            var azureInstance = _configuration["AzureAd:Instance"];
            if (!string.IsNullOrEmpty(azureInstance) &&
                azureInstance.Contains(AzureAuthorityHosts.AzureGovernment.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _authorityHost = AzureAuthorityHosts.AzureGovernment;
                Console.WriteLine("🏛️ Using Azure Government Cloud Authority Host.");
            }
            else
            {
                _authorityHost = AzureAuthorityHosts.AzurePublicCloud;
                Console.WriteLine("☁️ Using Azure Public Cloud Authority Host.");
            }
        }
    }
}
