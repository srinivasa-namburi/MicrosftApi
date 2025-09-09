// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Provides a shared SignalR HubConnection for the Blazor WebAssembly app.
/// Components should prefer a cascading HubConnection when available, and fall back to this service otherwise.
/// This service guarantees a single connection instance and manages starting it.
/// This service is designed to be resilient and never throw exceptions that would crash UI components.
/// </summary>
public sealed class SignalRConnectionService
{
    private readonly IAuthorizationApiClient _authorizationApiClient;
    private readonly IConfigurationApiClient _configurationApiClient;
    private readonly ILogger<SignalRConnectionService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private HubConnection? _connection;
    private DateTime? _lastConnectionAttempt;
    private readonly TimeSpan _connectionAttemptCooldown = TimeSpan.FromSeconds(5);

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
    /// Attempts to get or create a SignalR connection. This method is designed to be resilient
    /// and will return null instead of throwing exceptions that could crash UI components.
    /// Components should handle null return values gracefully and fall back to API-only mode.
    /// </summary>
    /// <param name="forceRefresh">Whether to force creation of a new connection even if one exists.</param>
    /// <returns>The active <see cref="HubConnection"/> if successful, or null if connection failed.</returns>
    public async Task<HubConnection?> TryGetOrCreateAsync(bool forceRefresh = false)
    {
        try
        {
            return await GetOrCreateAsync(forceRefresh);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR connection attempt failed, returning null for graceful degradation");
            return null;
        }
    }

    /// <summary>
    /// Gets the shared HubConnection, creating and starting it if needed.
    /// This method will refresh the connection if it's in a failed state.
    /// 
    /// WARNING: This method can throw exceptions. Most components should use TryGetOrCreateAsync() instead
    /// for better resilience. This method is kept for backward compatibility and specific use cases
    /// where the caller wants to handle exceptions explicitly.
    /// </summary>
    /// <param name="forceRefresh">Whether to force creation of a new connection even if one exists.</param>
    /// <returns>The active <see cref="HubConnection"/>.</returns>
    /// <exception cref="SignalRConnectionException">Thrown when connection fails after all retry attempts.</exception>
    public async Task<HubConnection> GetOrCreateAsync(bool forceRefresh = false)
    {
        _logger.LogInformation("GetOrCreateAsync: Entry (forceRefresh: {ForceRefresh})", forceRefresh);
        
        // Check if we have a working connection and no forced refresh
        if (!forceRefresh && _connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
        {
            _logger.LogInformation("GetOrCreateAsync: Using existing connection (State: {State})", _connection.State);
            return _connection;
        }

        // Prevent too frequent connection attempts
        if (_lastConnectionAttempt.HasValue && 
            DateTime.UtcNow - _lastConnectionAttempt.Value < _connectionAttemptCooldown)
        {
            _logger.LogInformation("GetOrCreateAsync: Checking rate limiting");
            if (_connection != null)
            {
                _logger.LogInformation("GetOrCreateAsync: Rate limited but returning existing connection");
                return _connection;
            }
            
            var cooldownException = new SignalRConnectionException(
                $"SignalR connection attempt too recent. Please wait {_connectionAttemptCooldown.TotalSeconds} seconds between attempts.", 
                SignalRConnectionErrorType.RateLimited);
            
            _logger.LogDebug("SignalR connection rate limited");
            throw cooldownException;
        }

        _logger.LogInformation("GetOrCreateAsync: Acquiring semaphore for connection creation");
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogInformation("GetOrCreateAsync: Semaphore acquired, proceeding with connection creation");
            _lastConnectionAttempt = DateTime.UtcNow;

            // Dispose existing connection if forced refresh or if it's in a failed state
            if (forceRefresh || _connection?.State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation("GetOrCreateAsync: Disposing existing connection");
                await DisposeConnectionAsync();
            }

            if (_connection == null)
            {
                _logger.LogInformation("GetOrCreateAsync: Creating new connection");
                _connection = await CreateConnectionAsync();
            }

            if (_connection.State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation("GetOrCreateAsync: Starting connection (Current state: {State})", _connection.State);
                await StartConnectionAsync(_connection);
            }

            _logger.LogInformation("GetOrCreateAsync: Returning connection (Final state: {State})", _connection.State);
            return _connection;
        }
        finally
        {
            _logger.LogInformation("GetOrCreateAsync: Releasing semaphore");
            _sync.Release();
        }
    }

    /// <summary>
    /// Creates a new HubConnection instance without starting it.
    /// </summary>
    /// <returns>A new HubConnection instance.</returns>
    /// <exception cref="SignalRConnectionException">Thrown when authentication fails or connection configuration is invalid.</exception>
    private async Task<HubConnection> CreateConnectionAsync()
    {
        _logger.LogInformation("CreateConnectionAsync: Starting to create SignalR connection");
        string? accessToken = null;
        string? apiAddress = null;

        try
        {
            _logger.LogInformation("CreateConnectionAsync: Getting API address and access token");
            // Get API address and access token
            apiAddress = await _authorizationApiClient.GetApiAddressAsync().ConfigureAwait(false);
            _logger.LogInformation("CreateConnectionAsync: Got API address: {ApiAddress}", apiAddress);
            
            accessToken = await _configurationApiClient.GetAccessTokenAsync().ConfigureAwait(false);
            _logger.LogInformation("CreateConnectionAsync: Got access token (length: {Length})", accessToken?.Length ?? 0);
            
            _logger.LogDebug("Creating SignalR connection to {ApiAddress}", apiAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get API address or access token for SignalR connection");
            throw new SignalRConnectionException(
                "Unable to authenticate for SignalR connection. Please ensure you are logged in and try again.", 
                SignalRConnectionErrorType.Authentication,
                ex);
        }

        _logger.LogInformation("CreateConnectionAsync: Building HubConnection to {Url}", $"{apiAddress}/hubs/notification-hub");

        var connection = new HubConnectionBuilder()
            .WithUrl($"{apiAddress}/hubs/notification-hub", options =>
            {
                _logger.LogInformation("CreateConnectionAsync: Configuring connection options");
                options.AccessTokenProvider = async () => 
                {
                    try
                    {
                        _logger.LogTrace("AccessTokenProvider: Getting fresh token for SignalR");
                        // Always get a fresh token to handle token refresh scenarios
                        var token = await _configurationApiClient.GetAccessTokenAsync().ConfigureAwait(false);
                        _logger.LogTrace("AccessTokenProvider: Got token (length: {Length})", token?.Length ?? 0);
                        return token;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AccessTokenProvider: Failed to get access token for SignalR connection");
                        return null;
                    }
                };
            })
            .WithStatefulReconnect()
            .WithAutomaticReconnect(new[] 
            {
                TimeSpan.Zero,        // Immediate first retry
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _logger.LogInformation("CreateConnectionAsync: HubConnection built successfully");

        // Add connection event handlers for better logging and monitoring
        connection.Closed += OnConnectionClosed;
        connection.Reconnecting += OnConnectionReconnecting;
        connection.Reconnected += OnConnectionReconnected;

        _logger.LogInformation("CreateConnectionAsync: Event handlers attached, returning connection");
        return connection;
    }

    /// <summary>
    /// Starts the given HubConnection.
    /// </summary>
    /// <param name="connection">The connection to start.</param>
    /// <exception cref="SignalRConnectionException">Thrown when the connection fails to start.</exception>
    private async Task StartConnectionAsync(HubConnection connection)
    {
        try
        {
            _logger.LogInformation("StartConnectionAsync: About to start SignalR connection (Current state: {State})", connection.State);
            await connection.StartAsync().ConfigureAwait(false);
            _logger.LogInformation("StartConnectionAsync: SignalR connection established successfully (State: {State})", connection.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartConnectionAsync: Failed to start SignalR HubConnection. Error details: {ErrorType} - {ErrorMessage}", 
                ex.GetType().Name, ex.Message);
            
            // Classify the error for better handling
            var errorType = ClassifyConnectionError(ex);
            var errorMessage = GetUserFriendlyErrorMessage(ex, errorType);
            
            throw new SignalRConnectionException(errorMessage, errorType, ex);
        }
    }

    /// <summary>
    /// Classifies connection errors into specific types for better error handling.
    /// </summary>
    /// <param name="exception">The original exception.</param>
    /// <returns>The classified error type.</returns>
    private static SignalRConnectionErrorType ClassifyConnectionError(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("403") => SignalRConnectionErrorType.Forbidden,
            HttpRequestException httpEx when httpEx.Message.Contains("401") => SignalRConnectionErrorType.Authentication,
            HttpRequestException httpEx when httpEx.Message.Contains("404") => SignalRConnectionErrorType.EndpointNotFound,
            TaskCanceledException => SignalRConnectionErrorType.Timeout,
            _ => SignalRConnectionErrorType.NetworkError
        };
    }

    /// <summary>
    /// Provides user-friendly error messages based on the error type.
    /// </summary>
    /// <param name="originalException">The original exception.</param>
    /// <param name="errorType">The classified error type.</param>
    /// <returns>A user-friendly error message.</returns>
    private static string GetUserFriendlyErrorMessage(Exception originalException, SignalRConnectionErrorType errorType)
    {
        return errorType switch
        {
            SignalRConnectionErrorType.Authentication => 
                "SignalR connection was unauthorized. Please log in again.",
            SignalRConnectionErrorType.Forbidden => 
                "SignalR connection was forbidden. This may indicate an authentication or authorization issue.",
            SignalRConnectionErrorType.EndpointNotFound => 
                "SignalR endpoint not found. The SignalR hub may not be available.",
            SignalRConnectionErrorType.Timeout => 
                "SignalR connection timed out. Please check your network connection.",
            SignalRConnectionErrorType.RateLimited => 
                "SignalR connection attempts are being rate limited. Please wait before retrying.",
            SignalRConnectionErrorType.NetworkError => 
                "SignalR connection failed due to a network error. Please check your connection and try again.",
            _ => 
                $"SignalR connection failed: {originalException.Message}"
        };
    }

    /// <summary>
    /// Refreshes the SignalR connection by disposing the current one and creating a new one.
    /// This is useful when authentication tokens have been refreshed.
    /// Returns null if the refresh fails instead of throwing exceptions.
    /// </summary>
    /// <returns>The new <see cref="HubConnection"/> if successful, or null if refresh failed.</returns>
    public async Task<HubConnection?> TryRefreshConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing SignalR connection");
            return await GetOrCreateAsync(forceRefresh: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh SignalR connection, returning null for graceful degradation");
            return null;
        }
    }

    /// <summary>
    /// Refreshes the SignalR connection by disposing the current one and creating a new one.
    /// This is useful when authentication tokens have been refreshed.
    /// 
    /// WARNING: This method can throw exceptions. Most components should use TryRefreshConnectionAsync() instead.
    /// </summary>
    /// <returns>The new <see cref="HubConnection"/>.</returns>
    /// <exception cref="SignalRConnectionException">Thrown when the refresh fails.</exception>
    public async Task<HubConnection> RefreshConnectionAsync()
    {
        _logger.LogInformation("Refreshing SignalR connection");
        return await GetOrCreateAsync(forceRefresh: true);
    }

    /// <summary>
    /// Disposes the current connection if it exists.
    /// </summary>
    public async Task DisposeConnectionAsync()
    {
        if (_connection != null)
        {
            try
            {
                // Remove event handlers before disposal
                _connection.Closed -= OnConnectionClosed;
                _connection.Reconnecting -= OnConnectionReconnecting;
                _connection.Reconnected -= OnConnectionReconnected;

                if (_connection.State == HubConnectionState.Connected)
                {
                    await _connection.StopAsync();
                }
                
                await _connection.DisposeAsync();
                _logger.LogDebug("SignalR connection disposed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing SignalR connection");
            }
            finally
            {
                _connection = null;
            }
        }
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "SignalR connection closed due to error: {ErrorType} - {ErrorMessage}", 
                exception.GetType().Name, exception.Message);
        }
        else
        {
            _logger.LogInformation("SignalR connection closed gracefully");
        }
    }

    private async Task OnConnectionReconnecting(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogInformation(exception, "SignalR connection reconnecting due to error: {ErrorType} - {ErrorMessage}", 
                exception.GetType().Name, exception.Message);
        }
        else
        {
            _logger.LogInformation("SignalR connection reconnecting...");
        }
    }

    private async Task OnConnectionReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR connection reconnected successfully (ConnectionId: {ConnectionId})", connectionId);
    }

    /// <summary>
    /// Determines whether the specified connection is the shared connection instance managed by this service.
    /// </summary>
    /// <param name="connection">The connection to check.</param>
    /// <returns>True if the connection is the shared instance; otherwise, false.</returns>
    public bool IsShared(HubConnection? connection) => connection != null && ReferenceEquals(connection, _connection);

    /// <summary>
    /// Gets the current state of the managed connection.
    /// </summary>
    public HubConnectionState? ConnectionState => _connection?.State;
}

/// <summary>
/// Represents a SignalR connection error with additional context for better error handling.
/// </summary>
public class SignalRConnectionException : Exception
{
    /// <summary>
    /// Gets the type of SignalR connection error.
    /// </summary>
    public SignalRConnectionErrorType ErrorType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The type of connection error.</param>
    public SignalRConnectionException(string message, SignalRConnectionErrorType errorType) : base(message)
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorType">The type of connection error.</param>
    /// <param name="innerException">The inner exception.</param>
    public SignalRConnectionException(string message, SignalRConnectionErrorType errorType, Exception innerException) 
        : base(message, innerException)
    {
        ErrorType = errorType;
    }
}

/// <summary>
/// Represents the type of SignalR connection error for better error handling and user feedback.
/// </summary>
public enum SignalRConnectionErrorType
{
    /// <summary>
    /// Authentication failed - user needs to log in again.
    /// </summary>
    Authentication,

    /// <summary>
    /// Connection was forbidden - authorization issue.
    /// </summary>
    Forbidden,

    /// <summary>
    /// SignalR endpoint was not found.
    /// </summary>
    EndpointNotFound,

    /// <summary>
    /// Connection timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Connection attempts are being rate limited.
    /// </summary>
    RateLimited,

    /// <summary>
    /// General network error.
    /// </summary>
    NetworkError
}
