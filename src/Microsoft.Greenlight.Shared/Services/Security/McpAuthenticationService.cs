// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;
using Orleans;

namespace Microsoft.Greenlight.Shared.Services.Security;

/// <summary>
/// Implementation of MCP authentication operations including extracting user identity and managing access tokens.
/// </summary>
public sealed class McpAuthenticationService : IMcpAuthenticationService
{
    private readonly IClusterClient _clusterClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthenticationService"/> class.
    /// </summary>
    /// <param name="clusterClient">The Orleans cluster client.</param>
    public McpAuthenticationService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    /// <inheritdoc/>
    public string? ExtractProviderSubjectId(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Try nameidentifier claim full (this is the default on unmodified Entra tokens)
        var nameId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(nameId))
        {
            return nameId;
        }

        // Try "sub" claim first (primary identifier)
        var sub = user.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return sub;
        }

        // Fall back to "oid" for backwards compatibility
        var oid = user.FindFirst("oid")?.Value;
        if (!string.IsNullOrWhiteSpace(oid))
        {
            return oid;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<string?> GetUserTokenAsync(string providerSubjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerSubjectId))
        {
            return null;
        }

        var grain = _clusterClient.GetGrain<IUserTokenStoreGrain>(providerSubjectId);
        var tokenDto = await grain.GetTokenAsync();

        return tokenDto?.AccessToken;
    }

    /// <inheritdoc/>
    public async Task StoreUserTokenAsync(string providerSubjectId, string token, DateTimeOffset? expiresOnUtc = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerSubjectId))
        {
            throw new ArgumentException("ProviderSubjectId cannot be null or empty.", nameof(providerSubjectId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        }

        var grain = _clusterClient.GetGrain<IUserTokenStoreGrain>(providerSubjectId);
        var tokenDto = new UserTokenDTO
        {
            ProviderSubjectId = providerSubjectId,
            AccessToken = token,
            ExpiresOnUtc = expiresOnUtc
        };

        await grain.SetTokenAsync(tokenDto);
    }
}
