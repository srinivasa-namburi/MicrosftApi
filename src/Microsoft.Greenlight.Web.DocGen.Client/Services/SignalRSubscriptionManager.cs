// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Manages SignalR handler registrations and group membership for a component instance.
/// Ensures handlers aren't double-registered on a shared HubConnection and that groups
/// are rejoined after reconnect.
/// </summary>
public sealed class SignalRSubscriptionManager : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly Dictionary<string, IDisposable> _handlerDisposables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _groups = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sync = new(1, 1);
    private bool _reconnectHooked;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRSubscriptionManager"/> class.
    /// </summary>
    /// <param name="connection">The HubConnection to manage.</param>
    public SignalRSubscriptionManager(HubConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Ensures that the HubConnection is started.
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
    /// </summary>
    public async Task JoinGroupAsync(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        await EnsureConnectedAsync().ConfigureAwait(false);

        if (_groups.Add(group))
        {
            await _connection.SendAsync("AddToGroup", group).ConfigureAwait(false);
        }

        HookReconnectIfNeeded();
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
                await _connection.SendAsync("RemoveFromGroup", group).ConfigureAwait(false);
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
            // Rejoin groups after reconnect
            foreach (var g in _groups)
            {
                try
                {
                    await _connection.SendAsync("AddToGroup", g).ConfigureAwait(false);
                }
                catch
                {
                    // ignore and try next
                }
            }
            return;
        };
    }

    /// <summary>
    /// Unregisters all handlers and leaves all tracked groups.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Dispose handlers owned by this manager
            foreach (var d in _handlerDisposables.Values)
            {
                d.Dispose();
            }
            _handlerDisposables.Clear();

            // Leave groups this instance joined
            if (_groups.Count > 0 && _connection.State != HubConnectionState.Disconnected)
            {
                foreach (var g in _groups)
                {
                    try
                    {
                        await _connection.SendAsync("RemoveFromGroup", g).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            _groups.Clear();
        }
        finally
        {
            _sync.Dispose();
        }
    }
}
