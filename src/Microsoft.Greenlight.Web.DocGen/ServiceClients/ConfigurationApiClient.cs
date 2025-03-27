using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

public class ConfigurationApiClient : BaseServiceClient<ConfigurationApiClient>, IConfigurationApiClient
{
    private readonly IOptionsMonitor<ServiceConfigurationOptions> _serviceConfigurationOptions;

    public ConfigurationApiClient(HttpClient httpClient, 
        ILogger<ConfigurationApiClient> logger, 
        AuthenticationStateProvider authStateProvider, 
        IOptionsMonitor<ServiceConfigurationOptions> serviceConfigurationOptions) : base(httpClient, logger, authStateProvider)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions;
    }

    public async Task<string?> GetAzureMapsKeyAsync()
    {
       var azureMapsKey = _serviceConfigurationOptions.CurrentValue.AzureMaps.Key;
       return azureMapsKey;
    }

    public async Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync()
    {
       var documentProcesses = _serviceConfigurationOptions.CurrentValue.GreenlightServices.DocumentProcesses;
       return documentProcesses;
    }

    public async Task RestartWorkersAsync()
    {
        var response = await SendPostRequestMessage("/api/configuration/restart-workers", null);
        response?.EnsureSuccessStatusCode();
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync()
    {
        return _serviceConfigurationOptions.CurrentValue.GreenlightServices.FeatureFlags;
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions> GetGreenlightServicesAsync()
    {
        return _serviceConfigurationOptions.CurrentValue.GreenlightServices;
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions> GetFrontEndAsync()
    {
        return _serviceConfigurationOptions.CurrentValue.GreenlightServices.FrontEnd;
    }

    public async Task<ServiceConfigurationOptions.OpenAiOptions> GetOpenAiOptionsAsync()
    {
        return _serviceConfigurationOptions.CurrentValue.OpenAi;
    }

    public async Task<DbConfigurationInfo> UpdateConfigurationAsync(ConfigurationUpdateRequest request)
    {
        var response = await SendPostRequestMessage("/api/configuration/update", request);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<DbConfigurationInfo>()!;
    }

    public async Task<DbConfigurationInfo> GetCurrentConfigurationAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/current");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<DbConfigurationInfo>()!;
    }

    
    public async Task<List<AiModelInfo>> GetAiModelsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ai-models");
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<AiModelInfo>>() ??
               new List<AiModelInfo>();
    }

    public async Task<AiModelInfo> GetAiModelByIdAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/configuration/ai-models/{id}");
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelInfo>() ??
               throw new IOException($"Could not get AI model with ID {id}");
    }

    public async Task<AiModelInfo> CreateAiModelAsync(AiModelInfo model)
    {
        var response = await SendPostRequestMessage("/api/configuration/ai-models", model);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelInfo>() ??
               throw new IOException("Failed to create AI model");
    }

    public async Task<AiModelInfo> UpdateAiModelAsync(AiModelInfo model)
    {
        var response = await SendPutRequestMessage($"/api/configuration/ai-models/{model.Id}", model);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelInfo>() ??
               throw new IOException($"Failed to update AI model with ID {model.Id}");
    }

    public async Task DeleteAiModelAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/configuration/ai-models/{id}");
        response?.EnsureSuccessStatusCode();
    }

    
    public async Task<List<AiModelDeploymentInfo>> GetAiModelDeploymentsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ai-model-deployments");
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<List<AiModelDeploymentInfo>>()! ??
               throw new IOException("No AI model deployments available!");
    }

    public async Task<AiModelDeploymentInfo> GetAiModelDeploymentByIdAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/configuration/ai-model-deployments/{id}");
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ??
               throw new IOException($"Could not get AI model deployment with ID {id}");
    }

    public async Task<AiModelDeploymentInfo> CreateAiModelDeploymentAsync(AiModelDeploymentInfo deployment)
    {
        var response = await SendPostRequestMessage("/api/configuration/ai-model-deployments", deployment);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ??
               throw new IOException("Failed to create AI model deployment");
    }

    public async Task<AiModelDeploymentInfo> UpdateAiModelDeploymentAsync(AiModelDeploymentInfo deployment)
    {
        var response = await SendPutRequestMessage($"/api/configuration/ai-model-deployments/{deployment.Id}", deployment);
        response?.EnsureSuccessStatusCode();
        return await response?.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ??
               throw new IOException($"Failed to update AI model deployment with ID {deployment.Id}");
    }

    public async Task DeleteAiModelDeploymentAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/configuration/ai-model-deployments/{id}");
        response?.EnsureSuccessStatusCode();
    }
}
