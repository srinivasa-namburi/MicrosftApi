using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Greenlight.Web.DocGen.Auth;

internal static class LoginLogoutEndpointRouteBuilderExtensions
{
    internal static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder endpoints, IApplicationBuilder app)
    {
        var group = endpoints.MapGroup("");

        // Authentication endpoint that immediately challenges
        group.MapGet("/login", Login)
            .AllowAnonymous()
            .WithName("LoginEndpoint");

        // Sign out of the Cookie and OIDC handlers
        group.MapPost("/logout", Logout)
            .WithName("LogoutEndpoint");

        // API endpoint to get the current authentication state
        group.MapGet("/user", async (HttpContext context) =>
        {
            var authenticateResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (!authenticateResult.Succeeded)
            {
                return Results.Unauthorized();
            }
            
            return Results.Ok(new
            {
                IsAuthenticated = true,
                UserName = authenticateResult.Principal?.Identity?.Name,
                Claims = authenticateResult.Principal?.Claims.Select(c => new { c.Type, c.Value })
            });
        }).RequireAuthorization()
        .WithName("CurrentUserEndpoint");

        return group;
    }

    private static ChallengeHttpResult Login(string? returnUrl)
    {
        var authProperties = GetAuthProperties(returnUrl);
        
        // Force immediate authentication challenge
        return TypedResults.Challenge(authProperties, new[] { "MicrosoftOidc" });
    }
    
    private static SignOutHttpResult Logout([FromForm] string? returnUrl)
    {
        var authProperties = GetAuthProperties(returnUrl);
        return TypedResults.SignOut(authProperties, [CookieAuthenticationDefaults.AuthenticationScheme, "MicrosoftOidc"]);
    }
    
    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        // Use HttpContext.Request.PathBase if available
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
            returnUrl = pathBase;
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        else if (returnUrl[0] != '/') 
            returnUrl = $"{pathBase}{returnUrl}";

        return new AuthenticationProperties { 
            RedirectUri = returnUrl,
            // Set IsPersistent to true to maintain the session after browser closes
            IsPersistent = true,
            // Force immediate challenge rather than redirect
            AllowRefresh = true
        };
    }
}
