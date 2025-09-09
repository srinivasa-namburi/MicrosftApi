// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Factory to create SignalRSubscriptionManager instances with proper logging support.
/// </summary>
public sealed class SignalRSubscriptionFactory
{
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRSubscriptionFactory"/> class.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory for creating loggers for subscription managers.</param>
    public SignalRSubscriptionFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new subscription manager instance bound to the provided connection.
    /// </summary>
    /// <param name="connection">The HubConnection to manage.</param>
    /// <returns>A new SignalRSubscriptionManager instance.</returns>
    public SignalRSubscriptionManager Create(HubConnection connection)
    {
        var logger = _loggerFactory?.CreateLogger<SignalRSubscriptionManager>();
        return new SignalRSubscriptionManager(connection, logger);
    }
}
