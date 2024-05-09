using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProjectVico.V2.Web.Shared.Auth;

// Add properties to this class and update the server and client AuthenticationStateProviders
// to expose more information about the authenticated user to the client.
public sealed class UserInfo
{
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; set; }
    public string? Token { get; set; }

    public const string UserIdClaimType = "sub";
    public const string NameClaimType = "name";
    public const string EmailClaimType = "preferred_username";
    public const string AccessTokenClaimType = "access_token";

    public static UserInfo FromClaimsPrincipal(ClaimsPrincipal principal, JwtSecurityToken contextSecurityToken) =>
        new()
        {
            UserId = GetRequiredClaim(principal, UserIdClaimType),
            Name = GetRequiredClaim(principal, NameClaimType),
            Email = GetRequiredClaim(principal, EmailClaimType),
            Token = contextSecurityToken.EncodedPayload
        };

    public static UserInfo FromClaimsPrincipal(ClaimsPrincipal principal) =>
        new()
        {
            UserId = GetRequiredClaim(principal, UserIdClaimType),
            Name = GetRequiredClaim(principal, NameClaimType),
            Email = GetRequiredClaim(principal, EmailClaimType),
            Token = GetOptionalClaim(principal, AccessTokenClaimType) 
        };


    public ClaimsPrincipal ToClaimsPrincipal() =>
        new(new ClaimsIdentity(
            [new(UserIdClaimType, UserId), new(NameClaimType, Name)],
            authenticationType: nameof(UserInfo),
            nameType: NameClaimType,
            roleType: null));

    private static string? GetOptionalClaim(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value;

    private static string GetRequiredClaim(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value ?? throw new InvalidOperationException($"Could not find required '{claimType}' claim.");
}