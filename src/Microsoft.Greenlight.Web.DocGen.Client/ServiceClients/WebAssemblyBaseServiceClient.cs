using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public abstract class WebAssemblyBaseServiceClient<T> where T : IServiceClient
{
    protected readonly ILogger<T> Logger;
    protected readonly HttpClient HttpClient;

    private readonly AuthenticationStateProvider _authStateProvider;

    protected WebAssemblyBaseServiceClient(HttpClient httpClient, ILogger<T> logger, AuthenticationStateProvider authStateProvider)
    {
        Logger = logger;
        _authStateProvider = authStateProvider;
        HttpClient = httpClient;
    }

    protected async Task<HttpResponseMessage?> SendGetRequestMessage(string requestUri, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            requestMessage.Headers.Authorization = new("Bearer", token);
        }

        Logger.LogInformation("Sending GET request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    protected async Task<HttpResponseMessage> SendDeleteRequestMessage(string requestUri, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            requestMessage.Headers.Authorization = new("Bearer", token);
        }

        Logger.LogInformation("Sending DELETE request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    protected async Task<HttpResponseMessage?> SendPostRequestMessage(string requestUri, object? payload, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            requestMessage.Headers.Authorization = new("Bearer", token);
        }

        if (payload is IBrowserFile file)
        {
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(file.OpenReadStream(file.Size));
            content.Add(streamContent, "file", file.Name);
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
            var token = await GetAccessTokenAsync();
            requestMessage.Headers.Authorization = new("Bearer", token);
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
        var response = await SendGetRequestMessage($"/configuration/token");

        if (response?.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Unauthorized access");
        }

        response?.EnsureSuccessStatusCode();

        var resultString = await response?.Content.ReadAsStringAsync()!;
        if (resultString == null)
        {
            throw new UnauthorizedAccessException("Unauthorized access");
        }

        return resultString;
    }
}