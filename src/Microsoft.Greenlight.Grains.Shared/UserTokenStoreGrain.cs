// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

namespace Microsoft.Greenlight.Grains.Shared;

/// <summary>
/// Grain that stores per-user bearer tokens by ProviderSubjectId (string key).
/// </summary>
public class UserTokenStoreGrain : Grain, IUserTokenStoreGrain
{
    private readonly IPersistentState<UserTokenDTO?> _state;
    private readonly ILogger<UserTokenStoreGrain> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTokenStoreGrain"/> class.
    /// </summary>
    public UserTokenStoreGrain(
        [PersistentState(stateName: "token", storageName: "PubSubStore")] IPersistentState<UserTokenDTO?> state,
        ILogger<UserTokenStoreGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetTokenAsync(UserTokenDTO token)
    {
        if (token is null || string.IsNullOrWhiteSpace(token.ProviderSubjectId) || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new ArgumentException("Invalid token payload", nameof(token));
        }

        // Ensure grain key matches the provided ProviderSubjectId to avoid accidental cross-user writes.
        var grainKey = this.GetPrimaryKeyString();
        if (!string.Equals(grainKey, token.ProviderSubjectId, StringComparison.Ordinal))
        {
            _logger.LogWarning("ProviderSubjectId mismatch for token upsert. GrainKey={GrainKey} PayloadKey={PayloadKey}", grainKey, token.ProviderSubjectId);
            throw new InvalidOperationException("ProviderSubjectId does not match the grain's primary key.");
        }

        _state.State = new UserTokenDTO
        {
            ProviderSubjectId = token.ProviderSubjectId,
            AccessToken = token.AccessToken,
            ExpiresOnUtc = token.ExpiresOnUtc
        };
        await _state.WriteStateAsync();
    }

    /// <inheritdoc />
    public Task<UserTokenDTO?> GetTokenAsync()
    {
        return Task.FromResult(_state.State);
    }
}
