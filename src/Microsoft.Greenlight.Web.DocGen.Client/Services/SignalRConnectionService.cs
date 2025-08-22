// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Provides a shared SignalR HubConnection for the Blazor WebAssembly app.
/// Components should prefer a cascading HubConnection when available, and fall back to this service otherwise.
/// This service guarantees a single connection instance and manages starting it.
/// </summary>
public sealed class SignalRConnectionService
{
    private readonly IAuthorizationApiClient _authorizationApiClient;
    private readonly IConfigurationApiClient _configurationApiClient;
    private readonly ILogger<SignalRConnectionService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private HubConnection? _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRConnectionService"/> class.
    /// </summary>
    /// <param name="authorizationApiClient">The authorization API client.</param>
    /// <param name="configurationApiClient">The configuration API client.</param>
    /// <param name="logger">The logger.</param>
    public SignalRConnectionService(
        IAuthorizationApiClient authorizationApiClient,
        IConfigurationApiClient configurationApiClient,
        ILogger<SignalRConnectionService> logger)
    {
        _authorizationApiClient = authorizationApiClient;
        _configurationApiClient = configurationApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets the shared HubConnection, creating and starting it if needed.
    /// </summary>
    /// <returns>The active <see cref="HubConnection"/>.</returns>
    public async Task<HubConnection> GetOrCreateAsync()
    {
        if (_connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
        {
            return _connection;
        }

        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection == null)
            {
                var apiAddress = await _authorizationApiClient.GetApiAddressAsync().ConfigureAwait(false);
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{apiAddress}/hubs/notification-hub", options =>
                    {
                        options.AccessTokenProvider = async () => await _configurationApiClient.GetAccessTokenAsync().ConfigureAwait(false);
                    })
                    .WithStatefulReconnect()
                    .WithAutomaticReconnect()
                    .Build();
            }

            if (_connection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await _connection.StartAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start SignalR HubConnection");
                    throw;
                }
            }

            return _connection;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Determines whether the specified connection is the shared connection instance managed by this service.
    /// </summary>
    /// <param name="connection">The connection to check.</param>
    /// <returns>True if the connection is the shared instance; otherwise, false.</returns>
    public bool IsShared(HubConnection? connection) => connection != null && ReferenceEquals(connection, _connection);
}
