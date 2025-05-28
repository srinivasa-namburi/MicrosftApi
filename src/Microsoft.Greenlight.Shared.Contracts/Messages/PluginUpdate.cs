// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.Messages
{
    /// <summary>
    /// Message published when a plugin version should be stopped and removed on all nodes.
    /// </summary>
    public record PluginUpdate(string PluginName, string VersionString, Guid CorrelationId);
}
