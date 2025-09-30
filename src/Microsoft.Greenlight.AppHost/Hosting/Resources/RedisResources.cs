// Copyright (c) Microsoft Corporation. All rights reserved.
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Microsoft.Greenlight.AppHost.Hosting.Resources;

/// <summary>
/// This class previously handled Azure Managed Redis configuration.
///
/// As of the Redis containerization update, we no longer use Azure Redis.
/// Instead, we deploy two Redis containers directly:
/// - "redis": Main Redis for caching, Orleans, data protection (4GB)
/// - "redis-signalr": Dedicated Redis for SignalR backplane (512MB)
///
/// Both are configured in AzureDependencies.cs with PublishAsContainer()
/// </summary>
[Obsolete("Azure Managed Redis is no longer used. Redis is now deployed as containers.")]
internal static class RedisResources
{
    // Kept for backwards compatibility during migration
    // This method is no longer called
    public static IResourceBuilder<IResourceWithConnectionString>? AddManagedRedisConnection(IDistributedApplicationBuilder builder)
    {
        throw new NotSupportedException(
            "Azure Managed Redis is no longer supported. " +
            "Redis is now deployed as containers. " +
            "See AzureDependencies.cs for the new configuration.");
    }
}
