using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IConfigurationApiClient : IServiceClient
{
    Task<string?> GetAzureMapsKeyAsync();
    Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync();

    Task<ServiceConfigurationOptions.ProjectVicoServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync();
}