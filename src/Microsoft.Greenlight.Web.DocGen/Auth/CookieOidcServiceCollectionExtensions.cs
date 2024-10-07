using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Microsoft.Greenlight.Web.DocGen.Auth;

internal static class CookieOidcServiceCollectionExtensions
{
    public static IServiceCollection ConfigureOidcRefreshHandling(this IServiceCollection services, string cookieScheme,
        string oidcScheme)
    {
        services.AddSingleton<OidcRefreshHandler>();
        services.AddOptions<CookieAuthenticationOptions>(cookieScheme).Configure<OidcRefreshHandler>(
            (cookieOptions, refresher) =>
            {
                cookieOptions.Events.OnValidatePrincipal =
                    context => refresher.ValidateOrRefreshCookieAsync(context, oidcScheme);
            });
        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
         
            // Request a refresh_token.
            oidcOptions.Scope.Add("offline_access");
            // Store the refresh_token.
            oidcOptions.SaveTokens = true;
        });
        return services;
    }
}
