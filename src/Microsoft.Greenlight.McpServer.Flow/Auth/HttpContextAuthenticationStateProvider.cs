// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Web.Shared.Auth;
using System.IdentityModel.Tokens.Jwt;

namespace Microsoft.Greenlight.McpServer.Flow.Auth;

/// <summary>
/// AuthenticationStateProvider implementation that reads the current ClaimsPrincipal
/// from IHttpContextAccessor and adds the Authorization Bearer token as a claim.
/// Server-only; used by MCP to reuse shared clients that expect AuthenticationStateProvider.
/// </summary>
public sealed class HttpContextAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpContextAuthenticationStateProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor to retrieve the current HTTP context and user.</param>
    public HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current authentication state from the HTTP context. If a Bearer token is present
    /// in the Authorization header, it is injected into the user's claims using
    /// <see cref="UserInfo.AccessTokenClaimType"/> and missing identity claims (sub/name/preferred_username)
    /// are populated by decoding the JWT, mirroring Web.DocGen claim shaping behavior.
    /// </summary>
    /// <returns>The current <see cref="AuthenticationState"/>.</returns>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var http = _httpContextAccessor.HttpContext;
        var principal = http?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

        var authHeader = http?.Request?.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (!string.IsNullOrEmpty(token))
            {
                // Ensure we have a ClaimsIdentity to enrich
                var baseIdentity = principal.Identity as ClaimsIdentity ?? new ClaimsIdentity(authenticationType: "Bearer");

                // Clone existing claims to avoid mutating original identity
                var enriched = new ClaimsIdentity(baseIdentity.Claims, baseIdentity.AuthenticationType, baseIdentity.NameClaimType, baseIdentity.RoleClaimType);

                // Add access token claim if missing
                if (!enriched.HasClaim(c => c.Type == UserInfo.AccessTokenClaimType))
                {
                    enriched.AddClaim(new Claim(UserInfo.AccessTokenClaimType, token));
                }

                // Decode JWT to pull standard claims when not already present
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    // ReadJwtToken does not validate; it's safe for extracting non-trusted claims for display/forwarding
                    var jwt = handler.ReadJwtToken(token);

                    // Helper local function to add a claim if missing and available in token
                    void TryAdd(string claimType, params string[] candidateTypes)
                    {
                        if (enriched.HasClaim(c => c.Type == claimType))
                        {
                            return;
                        }
                        foreach (var ct in candidateTypes)
                        {
                            var value = jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, ct, StringComparison.OrdinalIgnoreCase))?.Value;
                            if (!string.IsNullOrEmpty(value))
                            {
                                enriched.AddClaim(new Claim(claimType, value));
                                break;
                            }
                        }
                    }

                    // sub is required by UserInfo
                    TryAdd(UserInfo.UserIdClaimType, "sub", ClaimTypes.NameIdentifier, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier");
                    // name is required by UserInfo
                    TryAdd(UserInfo.NameClaimType, "name", ClaimTypes.Name, "preferred_username", "upn");
                    // preferred_username (email) is required by UserInfo
                    TryAdd(UserInfo.EmailClaimType, "preferred_username", ClaimTypes.Email, "upn", "email");
                }
                catch
                {
                    // If token cannot be parsed, we still return identity with the access token claim present
                }

                principal = new ClaimsPrincipal(enriched);
            }
        }

        return Task.FromResult(new AuthenticationState(principal));
    }
}
