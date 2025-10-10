using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IConfigurationApiClient : IServiceClient
{
    Task<string?> GetAzureMapsKeyAsync();
    Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync();
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync();
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.FrontendOptions> GetFrontEndAsync();
    Task<ServiceConfigurationOptions.OpenAiOptions> GetOpenAiOptionsAsync();
    Task<DbConfigurationInfo> UpdateConfigurationAsync(ConfigurationUpdateRequest request);
    Task<DbConfigurationInfo> GetCurrentConfigurationAsync();
    Task<List<AiModelInfo>> GetAiModelsAsync();
    Task<AiModelInfo> GetAiModelByIdAsync(Guid id);
    Task<AiModelInfo> CreateAiModelAsync(AiModelInfo model);
    Task<AiModelInfo> UpdateAiModelAsync(AiModelInfo model);
    Task DeleteAiModelAsync(Guid id);
    Task<List<AiModelDeploymentInfo>> GetAiModelDeploymentsAsync();
    Task<AiModelDeploymentInfo> GetAiModelDeploymentByIdAsync(Guid id);
    Task<AiModelDeploymentInfo> CreateAiModelDeploymentAsync(AiModelDeploymentInfo deployment);
    Task<AiModelDeploymentInfo> UpdateAiModelDeploymentAsync(AiModelDeploymentInfo deployment);
    Task DeleteAiModelDeploymentAsync(Guid id);
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.ScalabilityOptions> GetScalabilityOptionsAsync();
    /// <summary>
    /// Gets the vector store options.
    /// </summary>
    Task<VectorStoreOptions> GetVectorStoreOptionsAsync();

    /// <summary>
    /// Gets the Flow AI Assistant options.
    /// </summary>
    Task<ServiceConfigurationOptions.FlowOptions> GetFlowOptionsAsync();

    /// <summary>
    /// Gets the global options.
    /// </summary>
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.GlobalOptions> GetGlobalOptionsAsync();

    /// <summary>
    /// Starts an export job for a given index table (Postgres only).
    /// </summary>
    Task<IndexJobStartedResponse> StartIndexExportAsync(IndexExportRequest request);

    /// <summary>
    /// Starts an import job for a given index table (Postgres only).
    /// </summary>
    Task<IndexJobStartedResponse> StartIndexImportAsync(IndexImportRequest request);

    /// <summary>
    /// Gets the status of an export job.
    /// </summary>
    Task<IndexExportJobStatus> GetIndexExportStatusAsync(Guid jobId);

    /// <summary>
    /// Gets the status of an import job.
    /// </summary>
    Task<IndexImportJobStatus> GetIndexImportStatusAsync(Guid jobId);

    // Document Ingestion and OCR endpoints
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions> GetDocumentIngestionOptionsAsync();
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.DocumentIngestionOptions.OcrOptions> GetOcrOptionsAsync();
    Task<List<LanguageDisplayInfo>> GetCachedOcrLanguagesAsync();
    Task<OcrLanguageDownloadResponse> DownloadOcrLanguageAsync(string languageCode);
    Task<DbConfigurationInfo> SetDefaultOcrLanguagesAsync(List<string> languages);

    // Secret configuration override endpoints
    Task<SecretOverrideInfo> GetSecretOverrideInfoAsync(string configurationKey);
    Task<SecretOverrideInfo> SetSecretOverrideAsync(SecretOverrideRequest request);
    Task<SecretOverrideInfo> RemoveSecretOverrideAsync(string configurationKey);

    // System Prompt endpoints
    /// <summary>
    /// Gets a system prompt by name (with database override if exists).
    /// </summary>
    Task<SystemPromptResponse> GetSystemPromptAsync(string promptName);

    /// <summary>
    /// Gets the default (non-overridden) prompt text for a system prompt.
    /// </summary>
    Task<SystemPromptResponse> GetDefaultSystemPromptAsync(string promptName);

    /// <summary>
    /// Updates or creates a system prompt override.
    /// </summary>
    Task UpdateSystemPromptAsync(string promptName, string text, bool isActive = true);

    /// <summary>
    /// Deletes a system prompt override (reverts to default).
    /// </summary>
    Task DeleteSystemPromptOverrideAsync(string promptName);

    /// <summary>
    /// Validates that a prompt contains all required Scriban variables.
    /// </summary>
    Task<PromptValidationResult> ValidateSystemPromptAsync(string promptName, string text);

    /// <summary>
    /// Gets all available system prompt names.
    /// </summary>
    Task<List<string>> GetAvailableSystemPromptsAsync();
}
