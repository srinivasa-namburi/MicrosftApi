// Copyright (c) Microsoft Corporation. All rights reserved.
using Aspire.Hosting.Orleans;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// Orleans storage and clustering configuration
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Orleans resources result
    /// </summary>
    internal readonly struct OrleansResources
    {
        internal readonly OrleansService Orleans;
        internal readonly IResourceBuilder<IResourceWithConnectionString> ClusteringTable;
        internal readonly IResourceBuilder<IResourceWithConnectionString> BlobStorage;
        internal readonly IResourceBuilder<IResourceWithConnectionString> Checkpointing;

        internal OrleansResources(
            OrleansService orleans,
            IResourceBuilder<IResourceWithConnectionString> clusteringTable,
            IResourceBuilder<IResourceWithConnectionString> blobStorage,
            IResourceBuilder<IResourceWithConnectionString> checkpointing)
        {
            Orleans = orleans;
            ClusteringTable = clusteringTable;
            BlobStorage = blobStorage;
            Checkpointing = checkpointing;
        }
    }

    /// <summary>
    /// Sets up Orleans clustering and storage
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <param name="azureDependencies">Azure dependencies for storage</param>
    /// <returns>Configured Orleans resources</returns>
    internal static OrleansResources SetupOrleans(
        IDistributedApplicationBuilder builder,
        AzureDependencies azureDependencies)
    {
        IResourceBuilder<IResourceWithConnectionString> orleansClusteringTable;
        IResourceBuilder<IResourceWithConnectionString> orleansBlobStorage;
        IResourceBuilder<IResourceWithConnectionString> orleansCheckpointing;

        if (builder.ExecutionContext.IsRunMode) // For local development
        {
            var orleansStorage = builder
                .AddAzureStorage("orleans-storage")
                .RunAsEmulator(azurite =>
                {
                    azurite.WithDataVolume("pvico-orleans-emulator-storage");
                });

            orleansBlobStorage = orleansStorage
                .AddBlobs("blob-orleans");

            orleansClusteringTable = orleansStorage
                .AddTables("clustering");

            orleansCheckpointing = orleansStorage
                .AddTables("checkpointing");
        }
        else // For production/Azure deployment
        {
            var orleansStorage = builder.AddAzureStorage("orleans-storage");

            orleansBlobStorage = orleansStorage
                .AddBlobs("blob-orleans");

            orleansClusteringTable = orleansStorage
                .AddTables("clustering");

            orleansCheckpointing = orleansStorage
                .AddTables("checkpointing");
        }

        var orleans = builder.AddOrleans("default")
            .WithClustering(orleansClusteringTable)
            .WithClusterId("greenlight-cluster")
            .WithServiceId("greenlight-main-silo")
            .WithGrainStorage(orleansBlobStorage);

        return new OrleansResources(orleans, orleansClusteringTable, orleansBlobStorage, orleansCheckpointing);
    }
}