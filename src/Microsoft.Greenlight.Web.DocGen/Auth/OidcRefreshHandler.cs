using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Greenlight.Web.DocGen.Auth;

// https://github.com/dotnet/aspnetcore/issues/8175
internal sealed class OidcRefreshHandler : IDisposable
{
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptionsMonitor;
    private readonly ILogger<OidcRefreshHandler> _logger;

    private readonly OpenIdConnectProtocolValidator oidcTokenValidator = new OpenIdConnectProtocolValidator
    {
        // Refresh requests do not use the nonce parameter. Otherwise, we'd use oidcOptions.ProtocolValidator.
        RequireNonce = false
    };

    private readonly HttpClient refreshClient = new HttpClient();

    public OidcRefreshHandler(IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor, ILogger<OidcRefreshHandler> logger)
    {
        _oidcOptionsMonitor = oidcOptionsMonitor;
        _logger = logger;
    }

    public void Dispose()
    {
        refreshClient.Dispose();
    }

    public async Task ValidateOrRefreshCookieAsync(CookieValidatePrincipalContext validateContext, string oidcScheme)
    {
        var accessTokenExpirationText = validateContext.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(accessTokenExpirationText, out var accessTokenExpiration)) return;

        var oidcOptions = _oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider!.GetUtcNow();
        if (now + TimeSpan.FromMinutes(5) < accessTokenExpiration)
        {
            // If the access token doesn't expire within the next 5 minutes, we don't need to refresh it - exit here.
            return;
        }

        var oidcConfiguration = await oidcOptions.ConfigurationManager!.GetConfigurationAsync(validateContext.HttpContext.RequestAborted);
        var tokenEndpoint = oidcConfiguration.TokenEndpoint ?? throw new InvalidOperationException("Cannot refresh cookie. TokenEndpoint missing!");

        using var refreshResponse = await refreshClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = oidcOptions.ClientId,
            ["client_secret"] = oidcOptions.ClientSecret,
            ["scope"] = string.Join(" ", oidcOptions.Scope),
            ["refresh_token"] = validateContext.Properties.GetTokenValue("refresh_token")
        }));

        if (!refreshResponse.IsSuccessStatusCode)
        {
            validateContext.RejectPrincipal();
            return;
        }

        var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
        var message = new OpenIdConnectMessage(refreshJson);

        var validationParameters = oidcOptions.TokenValidationParameters.Clone();
        if (oidcOptions.ConfigurationManager is BaseConfigurationManager baseConfigurationManager)
        {
            validationParameters.ConfigurationManager = baseConfigurationManager;
        }
        else
        {
            validationParameters.ValidIssuer = oidcConfiguration.Issuer;
            validationParameters.IssuerSigningKeys = oidcConfiguration.SigningKeys;
        }

        var validationResult = await oidcOptions.TokenHandler.ValidateTokenAsync(message.IdToken, validationParameters);

        if (!validationResult.IsValid)
        {
            validateContext.RejectPrincipal();
            return;
        }

        // Create a new ClaimsIdentity with the refreshed access token
        var claimsIdentity = validateContext.Principal.Identity as ClaimsIdentity;
        if (claimsIdentity != null)
        {
            // Remove the old access_token if it exists
            var existingAccessTokenClaim = claimsIdentity.FindFirst("access_token");
            if (existingAccessTokenClaim != null)
            {
                claimsIdentity.RemoveClaim(existingAccessTokenClaim);
            }
            
            // Add the new access_token claim
            claimsIdentity.AddClaim(new Claim("access_token", message.AccessToken));
            
            // Optionally, you might want to refresh other claims as well based on the new token information
        }

        // Replace the principal in the context with the updated identity
        validateContext.ReplacePrincipal(new ClaimsPrincipal(claimsIdentity));

        // Indicate that the cookie should be renewed
        validateContext.ShouldRenew = true;

        var expiresIn = int.Parse(message.ExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var expiresAt = now + TimeSpan.FromSeconds(expiresIn);
        validateContext.Properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = message.AccessToken },
            new AuthenticationToken { Name = "id_token", Value = message.IdToken },
            new AuthenticationToken { Name = "refresh_token", Value = message.RefreshToken },
            new AuthenticationToken { Name = "token_type", Value = message.TokenType },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt.ToString("o", CultureInfo.InvariantCulture) },
            new AuthenticationToken { Name = "nonce", Value = message.Nonce }
        });
    }
}
