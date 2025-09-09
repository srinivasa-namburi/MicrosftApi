// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Manages SignalR handler registrations and group membership for a component instance.
/// Ensures handlers aren't double-registered on a shared HubConnection and that groups
/// are rejoined after reconnect. Designed to be resilient and handle connection failures gracefully.
/// </summary>
public sealed class SignalRSubscriptionManager : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly Dictionary<string, IDisposable> _handlerDisposables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _groups = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly ILogger<SignalRSubscriptionManager>? _logger;
    private bool _reconnectHooked;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRSubscriptionManager"/> class.
    /// </summary>
    /// <param name="connection">The HubConnection to manage.</param>
    /// <param name="logger">Optional logger for better diagnostics.</param>
    public SignalRSubscriptionManager(HubConnection connection, ILogger<SignalRSubscriptionManager>? logger = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger;
    }

    /// <summary>
    /// Ensures that the HubConnection is started. Returns true if successful, false if failed.
    /// This method will not throw exceptions to avoid crashing UI components.
    /// </summary>
    /// <returns>True if the connection is connected or successfully started; false otherwise.</returns>
    public async Task<bool> TryEnsureConnectedAsync()
    {
        try
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync().ConfigureAwait(false);
                _logger?.LogDebug("SignalR connection started successfully");
            }
            return _connection.State == HubConnectionState.Connected;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to ensure SignalR connection is started");
            return false;
        }
    }

    /// <summary>
    /// Ensures that the HubConnection is started.
    /// 
    /// WARNING: This method can throw exceptions. Most callers should use TryEnsureConnectedAsync() instead.
    /// </summary>
    public async Task EnsureConnectedAsync()
    {
        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Registers a handler ensuring only a single registration per method exists for this manager instance.
    /// This method is resilient and will not throw exceptions that could crash UI components.
    /// </summary>
    /// <returns>True if the handler was registered successfully; false otherwise.</returns>
    public bool TryRegisterHandlerOnce<T>(string methodName, Func<T, Task> asyncHandler)
    {
        try
        {
            RegisterHandlerOnce(methodName, asyncHandler);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register SignalR handler for method {MethodName}", methodName);
            return false;
        }
    }

    /// <summary>
    /// Registers a handler ensuring only a single registration per method exists for this manager instance.
    /// 
    /// WARNING: This method can throw exceptions. Most callers should use TryRegisterHandlerOnce() instead.
    /// </summary>
    public void RegisterHandlerOnce<T>(string methodName, Func<T, Task> asyncHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(asyncHandler);

        // Dispose any previous registration for this method for this instance
        if (_handlerDisposables.Remove(methodName, out var existing))
        {
            existing.Dispose();
        }

        // Register new one and keep the disposer
        var disposable = _connection.On<T>(methodName, asyncHandler);
        _handlerDisposables[methodName] = disposable;
    }

    /// <summary>
    /// Registers a 2-parameter handler ensuring only a single registration per method exists for this manager instance.
    /// This method is resilient and will not throw exceptions that could crash UI components.
    /// </summary>
    /// <returns>True if the handler was registered successfully; false otherwise.</returns>
    public bool TryRegisterHandlerOnce<T1, T2>(string methodName, Func<T1, T2, Task> asyncHandler)
    {
        try
        {
            RegisterHandlerOnce(methodName, asyncHandler);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register SignalR handler for method {MethodName}", methodName);
            return false;
        }
    }

    /// <summary>
    /// Registers a 2-parameter handler ensuring only a single registration per method exists for this manager instance.
    /// 
    /// WARNING: This method can throw exceptions. Most callers should use TryRegisterHandlerOnce() instead.
    /// </summary>
    public void RegisterHandlerOnce<T1, T2>(string methodName, Func<T1, T2, Task> asyncHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(asyncHandler);

        if (_handlerDisposables.Remove(methodName, out var existing))
        {
            existing.Dispose();
        }

        var disposable = _connection.On<T1, T2>(methodName, asyncHandler);
        _handlerDisposables[methodName] = disposable;
    }

    /// <summary>
    /// Joins a SignalR group and makes sure it will be rejoined after reconnects.
    /// This method is resilient and will not throw exceptions that could crash UI components.
    /// </summary>
    /// <param name="group">The group name to join.</param>
    /// <returns>True if successfully joined the group; false otherwise.</returns>
    public async Task<bool> TryJoinGroupAsync(string group)
    {
        try
        {
            await JoinGroupAsync(group);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to join SignalR group {GroupName}", group);
            return false;
        }
    }

    /// <summary>
    /// Joins a SignalR group and makes sure it will be rejoined after reconnects.
    /// 
    /// WARNING: This method can throw exceptions. Most callers should use TryJoinGroupAsync() instead.
    /// </summary>
    public async Task JoinGroupAsync(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        await EnsureConnectedAsync().ConfigureAwait(false);

        if (_groups.Add(group))
        {
            await _connection.SendAsync("AddToGroup", group).ConfigureAwait(false);
            _logger?.LogDebug("Joined SignalR group {GroupName}", group);
        }

        HookReconnectIfNeeded();
    }

    /// <summary>
    /// Leaves a SignalR group and stops tracking it for reconnects.
    /// This method is resilient and will not throw exceptions.
    /// </summary>
    /// <param name="group">The group name to leave.</param>
    /// <returns>True if successfully left the group; false otherwise.</returns>
    public async Task<bool> TryLeaveGroupAsync(string group)
    {
        try
        {
            await LeaveGroupAsync(group);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to leave SignalR group {GroupName}", group);
            return false;
        }
    }

    /// <summary>
    /// Leaves a SignalR group and stops tracking it for reconnects.
    /// </summary>
    public async Task LeaveGroupAsync(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }

        if (_groups.Remove(group))
        {
            if (_connection.State != HubConnectionState.Disconnected)
            {
                try
                {
                    await _connection.SendAsync("RemoveFromGroup", group).ConfigureAwait(false);
                    _logger?.LogDebug("Left SignalR group {GroupName}", group);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to send leave group message for {GroupName}, but removed from tracking", group);
                }
            }
        }
    }

    private void HookReconnectIfNeeded()
    {
        if (_reconnectHooked)
        {
            return;
        }

        _reconnectHooked = true;
        _connection.Reconnected += async _ =>
        {
            _logger?.LogInformation("SignalR reconnected, rejoining {GroupCount} groups", _groups.Count);
            
            // Rejoin groups after reconnect
            foreach (var g in _groups)
            {
                try
                {
                    await _connection.SendAsync("AddToGroup", g).ConfigureAwait(false);
                    _logger?.LogDebug("Rejoined SignalR group {GroupName} after reconnect", g);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to rejoin SignalR group {GroupName} after reconnect", g);
                    // Don't stop trying other groups if one fails
                }
            }
            return;
        };
    }

    /// <summary>
    /// Unregisters all handlers and leaves all tracked groups.
    /// This method is designed to be safe and will not throw exceptions during disposal.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Dispose handlers owned by this manager
            foreach (var d in _handlerDisposables.Values)
            {
                try
                {
                    d.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing SignalR handler during cleanup");
                }
            }
            _handlerDisposables.Clear();

            // Leave groups this instance joined
            if (_groups.Count > 0 && _connection.State != HubConnectionState.Disconnected)
            {
                _logger?.LogDebug("Leaving {GroupCount} SignalR groups during disposal", _groups.Count);
                
                foreach (var g in _groups)
                {
                    try
                    {
                        await _connection.SendAsync("RemoveFromGroup", g).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to leave SignalR group {GroupName} during disposal", g);
                        // Continue with other groups even if one fails
                    }
                }
            }
            _groups.Clear();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during SignalRSubscriptionManager disposal");
        }
        finally
        {
            _sync.Dispose();
        }
    }
}
