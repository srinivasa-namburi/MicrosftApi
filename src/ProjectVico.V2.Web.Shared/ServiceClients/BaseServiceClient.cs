using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectVico.V2.Web.Shared.Auth;

namespace ProjectVico.V2.Web.Shared.ServiceClients;


public interface IServiceClient
{
    Task<string> GetAccessTokenAsync();
}

public abstract class BaseServiceClient<T> where T : IServiceClient
{
    protected readonly ILogger<T> Logger;
    protected readonly HttpClient HttpClient;

    private readonly AuthenticationStateProvider _asp;
    
    protected string AccessToken => GetAccessTokenAsync().GetAwaiter().GetResult();

    protected BaseServiceClient(HttpClient httpClient, AuthenticationStateProvider asp, ILogger<T> logger)
    {
        Logger = logger;
        HttpClient = httpClient;
        _asp = asp;
    }

    protected async Task<HttpResponseMessage?> SendGetRequestMessage(string requestUri)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        requestMessage.Headers.Authorization = new("Bearer", this.AccessToken);

        Logger.LogInformation("Sending GET request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    protected async Task<HttpResponseMessage> SendDeleteRequestMessage(string requestUri)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        requestMessage.Headers.Authorization = new ("Bearer", this.AccessToken);

        Logger.LogInformation("Sending DELETE request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }
    

    protected async Task<HttpResponseMessage?> SendPostRequestMessage(string requestUri, object? pocoPayload, bool authorize = true)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (authorize)
        {
            requestMessage.Headers.Authorization = new("Bearer", this.AccessToken);
        }

        if (pocoPayload == null)
        {
            Logger.LogWarning("Sending POST request to {RequestUri} with empty payload", requestUri);

        }
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(pocoPayload), Encoding.UTF8, "application/json");

        Logger.LogInformation("Sending POST request to {RequestUri}", requestUri);
        
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var authenticationState = await _asp.GetAuthenticationStateAsync();
        var userInfo = UserInfo.FromClaimsPrincipal(authenticationState.User);

        var accessToken = userInfo.Token;
        return accessToken ?? throw new InvalidOperationException("No access_token was saved");
    }


}

