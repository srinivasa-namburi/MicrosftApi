// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Claims;

namespace Microsoft.Greenlight.Shared.Services.Security;

/// <summary>
/// Service for MCP authentication operations including extracting user identity and managing access tokens.
/// </summary>
public interface IMcpAuthenticationService
{
    /// <summary>
    /// Extracts the ProviderSubjectId from a ClaimsPrincipal.
    /// Checks "sub" claim first, falls back to "oid" for backwards compatibility.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The ProviderSubjectId or null if not found.</returns>
    string? ExtractProviderSubjectId(ClaimsPrincipal user);

    /// <summary>
    /// Gets the stored access token for a given ProviderSubjectId.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token or null if not found.</returns>
    Task<string?> GetUserTokenAsync(string providerSubjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an access token for a given ProviderSubjectId.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject identifier.</param>
    /// <param name="token">The access token to store.</param>
    /// <param name="expiresOnUtc">Optional expiration time for the token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreUserTokenAsync(string providerSubjectId, string token, DateTimeOffset? expiresOnUtc = null, CancellationToken cancellationToken = default);
}
