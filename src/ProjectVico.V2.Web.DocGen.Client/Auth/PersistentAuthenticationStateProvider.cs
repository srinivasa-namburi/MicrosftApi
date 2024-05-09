using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Web.Shared.Auth;
using System.Security.Claims;

namespace ProjectVico.V2.Web.DocGen.Client.Auth;

internal class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> defaultUnauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> authenticationStateTask = defaultUnauthenticatedTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState state)
    {
        if (!state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) || userInfo is null)
        {
            return;
        }

        var claims = new[]
        {
            new Claim("name", userInfo.Name),
            new Claim("preferred_username", userInfo.Email),
            new Claim("access_token", userInfo.Token ?? string.Empty),
            new Claim("sub", userInfo.UserId),
            new Claim(ClaimTypes.Email, userInfo.Email)
        };


        authenticationStateTask = Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims,
                authenticationType: nameof(PersistentAuthenticationStateProvider)))));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => authenticationStateTask;
}
