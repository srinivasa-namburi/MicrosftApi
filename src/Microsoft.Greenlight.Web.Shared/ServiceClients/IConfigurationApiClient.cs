using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IConfigurationApiClient : IServiceClient
{
    Task<string?> GetAzureMapsKeyAsync();
    Task<List<DocumentProcessOptions?>> GetDocumentProcessesAsync();

    Task<ServiceConfigurationOptions.GreenlightServicesOptions.FeatureFlagsOptions> GetFeatureFlagsAsync();
}
