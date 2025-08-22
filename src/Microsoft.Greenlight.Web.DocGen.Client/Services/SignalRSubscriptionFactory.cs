// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.AspNetCore.SignalR.Client;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Small factory to create a SignalRSubscriptionManager. Kept separate to allow DI later if needed.
/// </summary>
public sealed class SignalRSubscriptionFactory
{
    /// <summary>
    /// Creates a new subscription manager instance bound to the provided connection.
    /// </summary>
    public SignalRSubscriptionManager Create(HubConnection connection) => new(connection);
}
