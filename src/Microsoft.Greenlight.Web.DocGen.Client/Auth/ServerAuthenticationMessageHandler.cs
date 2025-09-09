// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Microsoft.Greenlight.Web.DocGen.Client.Auth;

/// <summary>
/// A DelegatingHandler that adds the authorization header with the access token from the persistent authentication state.
/// This is used for server-side pre-rendered Blazor WebAssembly applications.
/// </summary>
public class ServerAuthenticationMessageHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAuthenticationMessageHandler"/> class.
    /// </summary>
    /// <param name="authenticationStateProvider">The authentication state provider.</param>
    public ServerAuthenticationMessageHandler(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    /// <summary>
    /// Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.
    /// Adds the authorization header with the access token if available.
    /// </summary>
    /// <param name="request">The HTTP request message to send to the server.</param>
    /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var accessToken = user.FindFirst("access_token")?.Value;
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}