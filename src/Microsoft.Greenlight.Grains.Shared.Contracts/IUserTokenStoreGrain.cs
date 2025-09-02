// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

/// <summary>
/// Grain contract for storing and retrieving per-user access tokens used to call downstream services.
/// </summary>
public interface IUserTokenStoreGrain : IGrainWithStringKey
{
    /// <summary>
    /// Upserts a user token for the ProviderSubjectId.
    /// </summary>
    /// <param name="token">The token payload.</param>
    Task SetTokenAsync(UserTokenDTO token);

    /// <summary>
    /// Gets a user token by ProviderSubjectId (grain key).
    /// </summary>
    /// <returns>The stored token or null if missing.</returns>
    Task<UserTokenDTO?> GetTokenAsync();
}
