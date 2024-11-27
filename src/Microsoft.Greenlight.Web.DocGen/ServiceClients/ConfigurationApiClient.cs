using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

public class ConfigurationApiClient : BaseServiceClient<ConfigurationApiClient>, IConfigurationApiClient
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public ConfigurationApiClient(HttpClient httpClient, ILogger<ConfigurationApiClient> logger, AuthenticationStateProvider authStateProvider, IOptions<ServiceConfigurationOptions> serviceConfigurationOptions) : base(httpClient, logger, authStateProvider)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public async Task<string?> GetAzureMapsKeyAsync()
    {
       var azureMapsKey = _serviceConfigurationOptions.AzureMaps.Key;
       return azureMapsKey;
    }

    public async Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync()
    {
       var documentProcesses = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
       return documentProcesses;
    }

    public async Task RestartWorkersAsync()
    {
        var response = await SendPostRequestMessage("/api/configuration/restart-workers", null);
        response?.EnsureSuccessStatusCode();
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync()
    {
        return _serviceConfigurationOptions.GreenlightServices.FeatureFlags;
    }
}
