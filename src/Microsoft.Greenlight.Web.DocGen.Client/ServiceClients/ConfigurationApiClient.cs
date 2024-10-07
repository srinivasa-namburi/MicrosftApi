using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class ConfigurationApiClient : WebAssemblyBaseServiceClient<ConfigurationApiClient>, IConfigurationApiClient
{
    public ConfigurationApiClient(HttpClient httpClient, ILogger<ConfigurationApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string> GetCurrentAccessTokenAsync()
    {
        return await base.GetAccessTokenAsync();

    }

    public async Task<string?> GetAzureMapsKeyAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/azure-maps-key");
        response?.EnsureSuccessStatusCode();

        var responseString = await response?.Content.ReadAsStringAsync()!;
        responseString = responseString.Replace("\"", "").TrimEnd('/');
        return responseString;
    }

    public async Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/document-processes");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<DocumentProcessOptions>>()! ??
               throw new IOException("No document processes available!");
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/feature-flags");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions>()! ??
               throw new IOException("No feature flags available!");
    }
}
