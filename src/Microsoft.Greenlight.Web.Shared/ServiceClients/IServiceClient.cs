namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IServiceClient
{
    Task<string> GetAccessTokenAsync();
}
