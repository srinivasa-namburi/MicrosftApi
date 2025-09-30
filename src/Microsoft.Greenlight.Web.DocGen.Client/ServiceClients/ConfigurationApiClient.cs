using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class ConfigurationApiClient : WebAssemblyBaseServiceClient<ConfigurationApiClient>, IConfigurationApiClient
{
    public ConfigurationApiClient(HttpClient httpClient, ILogger<ConfigurationApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<string?> GetAzureMapsKeyAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/azure-maps-key", authorize: true);
        response?.EnsureSuccessStatusCode();

        var responseString = await response?.Content.ReadAsStringAsync()!;
        responseString = responseString.Replace("\"", "").TrimEnd('/');
        return responseString;
    }

    public async Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/document-processes", authorize: true);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<List<DocumentProcessOptions>>()! ??
               throw new IOException("No document processes available!");
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/feature-flags", authorize: true);
        response?.EnsureSuccessStatusCode();

        return await response?.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions>()! ??
               throw new IOException("No feature flags available!");
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.ScalabilityOptions> GetScalabilityOptionsAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/scalability-options", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response?.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.ScalabilityOptions>()! ??
               throw new IOException("No Greenlight services available!");
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions> GetFrontEndAsync()
    {
        var response = await SendGetRequestMessage($"/api/configuration/frontend", authorize: false);
        response?.EnsureSuccessStatusCode();

        return await response?.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions>()! ??
               throw new IOException("No frontend options available!");
    }

    public async Task<ServiceConfigurationOptions.OpenAiOptions> GetOpenAiOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/openai-options");
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.OpenAiOptions>()! ??
               throw new IOException("No OpenAI options available!");
    }

    public async Task<VectorStoreOptions> GetVectorStoreOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/vector-store-options", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<VectorStoreOptions>() ??
               new VectorStoreOptions();
    }

    public async Task<ServiceConfigurationOptions.FlowOptions> GetFlowOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/flow-options", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.FlowOptions>() ??
               new ServiceConfigurationOptions.FlowOptions();
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions> GetGlobalOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/global-options", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions>() ??
               new ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions();
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions> GetDocumentIngestionOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/document-ingestion-options", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions>() ??
               new ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions();
    }

    public async Task<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions.OcrOptions> GetOcrOptionsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ocr-options", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content
                   .ReadFromJsonAsync<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions.OcrOptions>() ??
               new ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions.OcrOptions();
    }

    public async Task<List<LanguageDisplayInfo>> GetCachedOcrLanguagesAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ocr/languages", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LanguageDisplayInfo>>() ?? new List<LanguageDisplayInfo>();
    }

    public async Task<OcrLanguageDownloadResponse> DownloadOcrLanguageAsync(string languageCode)
    {
        var response = await SendPostRequestMessage($"/api/configuration/ocr/download?language={Uri.EscapeDataString(languageCode)}", new { }, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OcrLanguageDownloadResponse>() ?? new OcrLanguageDownloadResponse { Language = languageCode };
    }

    public async Task<DbConfigurationInfo> SetDefaultOcrLanguagesAsync(List<string> languages)
    {
        var response = await SendPostRequestMessage("/api/configuration/ocr/default-languages", languages, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DbConfigurationInfo>() ?? throw new IOException("No configuration info returned!");
    }

    public async Task<DbConfigurationInfo> UpdateConfigurationAsync(ConfigurationUpdateRequest request)
    {
        var response = await SendPostRequestMessage("/api/configuration/update", request, authorize: true);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Logger.LogError("Configuration update failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Configuration update failed: {response.StatusCode} - {errorContent}");
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DbConfigurationInfo>() ?? throw new IOException("No configuration info returned!");
    }

    public async Task<DbConfigurationInfo> GetCurrentConfigurationAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/current", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DbConfigurationInfo>() ?? throw new IOException("No configuration info returned!");
    }

    public async Task<List<AiModelInfo>> GetAiModelsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ai-models", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AiModelInfo>>() ?? new List<AiModelInfo>();
    }

    public async Task<AiModelInfo> GetAiModelByIdAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/configuration/ai-models/{id}", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelInfo>() ?? throw new IOException("Model not found!");
    }

    public async Task<AiModelInfo> CreateAiModelAsync(AiModelInfo model)
    {
        var response = await SendPostRequestMessage("/api/configuration/ai-models", model, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelInfo>() ?? throw new IOException("Create failed!");
    }

    public async Task<AiModelInfo> UpdateAiModelAsync(AiModelInfo model)
    {
        var response = await SendPutRequestMessage($"/api/configuration/ai-models/{model.Id}", model, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelInfo>() ?? throw new IOException("Update failed!");
    }

    public async Task DeleteAiModelAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/configuration/ai-models/{id}", authorize: true);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<AiModelDeploymentInfo>> GetAiModelDeploymentsAsync()
    {
        var response = await SendGetRequestMessage("/api/configuration/ai-model-deployments", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AiModelDeploymentInfo>>() ?? new List<AiModelDeploymentInfo>();
    }

    public async Task<AiModelDeploymentInfo> GetAiModelDeploymentByIdAsync(Guid id)
    {
        var response = await SendGetRequestMessage($"/api/configuration/ai-model-deployments/{id}", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ?? throw new IOException("Deployment not found!");
    }

    public async Task<AiModelDeploymentInfo> CreateAiModelDeploymentAsync(AiModelDeploymentInfo deployment)
    {
        var response = await SendPostRequestMessage("/api/configuration/ai-model-deployments", deployment, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ?? throw new IOException("Create failed!");
    }

    public async Task<AiModelDeploymentInfo> UpdateAiModelDeploymentAsync(AiModelDeploymentInfo deployment)
    {
        var response = await SendPutRequestMessage($"/api/configuration/ai-model-deployments/{deployment.Id}", deployment, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiModelDeploymentInfo>() ?? throw new IOException("Update failed!");
    }

    public async Task DeleteAiModelDeploymentAsync(Guid id)
    {
        var response = await SendDeleteRequestMessage($"/api/configuration/ai-model-deployments/{id}", authorize: true);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IndexJobStartedResponse> StartIndexExportAsync(IndexExportRequest request)
    {
        var response = await SendPostRequestMessage("/api/configuration/indexes/export", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexJobStartedResponse>() ?? throw new IOException("No JobId returned from export start.");
    }

    public async Task<IndexJobStartedResponse> StartIndexImportAsync(IndexImportRequest request)
    {
        var response = await SendPostRequestMessage("/api/configuration/indexes/import", request, authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexJobStartedResponse>() ?? throw new IOException("No JobId returned from import start.");
    }

    public async Task<IndexExportJobStatus> GetIndexExportStatusAsync(Guid jobId)
    {
        var response = await SendGetRequestMessage($"/api/configuration/indexes/export/{jobId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexExportJobStatus>() ?? new IndexExportJobStatus();
    }

    public async Task<IndexImportJobStatus> GetIndexImportStatusAsync(Guid jobId)
    {
        var response = await SendGetRequestMessage($"/api/configuration/indexes/import/{jobId}", authorize: true);
        response?.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IndexImportJobStatus>() ?? new IndexImportJobStatus();
    }

    public async Task<SecretOverrideInfo> GetSecretOverrideInfoAsync(string configurationKey)
    {
        var response = await SendGetRequestMessage($"/api/configuration/secret-override?configurationKey={Uri.EscapeDataString(configurationKey)}", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SecretOverrideInfo>() ?? 
               throw new IOException("Failed to get secret override info!");
    }

    public async Task<SecretOverrideInfo> SetSecretOverrideAsync(SecretOverrideRequest request)
    {
        var response = await SendPostRequestMessage("/api/configuration/secret-override", request, authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SecretOverrideInfo>() ?? 
               throw new IOException("Failed to set secret override!");
    }

    public async Task<SecretOverrideInfo> RemoveSecretOverrideAsync(string configurationKey)
    {
        var response = await SendDeleteRequestMessage($"/api/configuration/secret-override?configurationKey={Uri.EscapeDataString(configurationKey)}", authorize: true);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SecretOverrideInfo>() ?? 
               throw new IOException("Failed to remove secret override!");
    }
}
