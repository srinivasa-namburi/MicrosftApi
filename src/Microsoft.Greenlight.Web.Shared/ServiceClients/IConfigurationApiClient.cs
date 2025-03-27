using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IConfigurationApiClient : IServiceClient
{
    Task<string?> GetAzureMapsKeyAsync();
    Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync();

    Task RestartWorkersAsync();
    Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync();

    Task<ServiceConfigurationOptions.GreenlightServicesOptions> GetGreenlightServicesAsync();
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
}
