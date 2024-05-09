using HandlebarsDotNet;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

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
       var documentProcesses = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;
       return documentProcesses;
    }
}