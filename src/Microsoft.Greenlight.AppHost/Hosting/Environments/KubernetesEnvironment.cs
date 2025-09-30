// Copyright (c) Microsoft Corporation. All rights reserved.
using Aspire.Hosting.Kubernetes;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// Kubernetes environment setup with Helm chart metadata
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Configures the Kubernetes environment for compute resources
    /// </summary>
    /// <param name="builder">The distributed application builder</param>
    /// <returns>The Kubernetes environment resource</returns>
    internal static IResourceBuilder<KubernetesEnvironmentResource> SetupKubernetesEnvironment(IDistributedApplicationBuilder builder)
    {
        var k8s = builder.AddKubernetesEnvironment("k8s");
        
        // Set basic Helm chart metadata; names can be refined later via config
        k8s.Resource.HelmChartName = "greenlight";
        k8s.Resource.HelmChartVersion = "0.1.0";
        k8s.Resource.HelmChartDescription = "Microsoft Greenlight";

        return k8s;
    }
}