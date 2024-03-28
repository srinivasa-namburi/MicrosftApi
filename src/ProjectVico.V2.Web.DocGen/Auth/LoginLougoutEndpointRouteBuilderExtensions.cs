using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ProjectVico.V2.Web.DocGen.Auth;

internal static class LoginLogoutEndpointRouteBuilderExtensions
{
    internal static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder endpoints, IApplicationBuilder app)
    {

        var group = endpoints.MapGroup("");

        group.MapGet("/login", Login)
            .AllowAnonymous();

        // Sign out of the Cookie and OIDC handlers. If you do not sign out with the OIDC handler,
        // the user will automatically be signed back in the next time they visit a page that requires authentication
        // without being able to choose another account.
        group.MapPost("/logout", Logout);

        return group;
    }

    private static ChallengeHttpResult Login(string? returnUrl)
    {
        var authProperties = GetAuthProperties(returnUrl);
        var challengeResult = TypedResults.Challenge(authProperties);
        
        return challengeResult;
    }
    
    private static SignOutHttpResult Logout([FromForm] string? returnUrl)
    {
        var authProperties = GetAuthProperties(returnUrl);
        return TypedResults.SignOut(authProperties, ["Cookies", "MicrosoftOidc"]);
    }
    
    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        // TODO: Use HttpContext.Request.PathBase instead.
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
            returnUrl = pathBase;
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        else if (returnUrl[0] != '/') returnUrl = $"{pathBase}{returnUrl}";

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}