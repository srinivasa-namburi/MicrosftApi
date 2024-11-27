using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Web.Shared.Auth;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

public abstract class BaseServiceClient<T> where T : IServiceClient
{
    private readonly ILogger<T> Logger;
    private readonly HttpClient HttpClient;

    private readonly AuthenticationStateProvider _authStateProvider;
    
    public string AccessToken => GetAccessTokenAsync().GetAwaiter().GetResult();

    protected BaseServiceClient(HttpClient httpClient, ILogger<T> logger, AuthenticationStateProvider authStateProvider)
    {
        Logger = logger;
        _authStateProvider = authStateProvider;
        HttpClient = httpClient;
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
    

    protected async Task<HttpResponseMessage?> SendPostRequestMessage(string requestUri, object? payload, bool authorize = true)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (authorize)
        {
            requestMessage.Headers.Authorization = new("Bearer", this.AccessToken);
        }

        if (payload is IFormFile file)
        {
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(file.OpenReadStream());
            content.Add(streamContent, "file", file.FileName);
            requestMessage.Content = content;
        }
        else if (payload != null)
        {
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        }
        else
        {
            Logger.LogWarning("Sending POST request to {RequestUri} with empty payload", requestUri);
        }

        Logger.LogInformation("Sending POST request to {RequestUri}", requestUri);
    
        return await HttpClient.SendAsync(requestMessage);
    }

    protected async Task<HttpResponseMessage?> SendPutRequestMessage(string requestUri, object? pocoPayload, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri);
        if (authorize)
        {
            requestMessage.Headers.Authorization = new("Bearer", this.AccessToken);
        }

        if (pocoPayload == null)
        {
            Logger.LogWarning("Sending PUT request to {RequestUri} with empty payload", requestUri);
        }

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(pocoPayload), Encoding.UTF8, "application/json");

        Logger.LogInformation("Sending PUT request to {RequestUri}", requestUri);
        
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var authInfo = await _authStateProvider.GetAuthenticationStateAsync();
        if (!authInfo.User.Identity!.IsAuthenticated)
        {
            throw new InvalidOperationException("The user is not authenticated");
        }
        var userInfo = UserInfo.FromClaimsPrincipal(authInfo.User);

        var jwtToken = new JwtSecurityToken(userInfo.Token);

        var token = jwtToken.RawData;
        return token ?? throw new InvalidOperationException("No access_token was saved");
    }
}