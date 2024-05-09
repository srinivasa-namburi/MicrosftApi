namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IServiceClient
{
    Task<string> GetAccessTokenAsync();
}