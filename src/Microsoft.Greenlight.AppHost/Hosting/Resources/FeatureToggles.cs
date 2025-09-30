// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// Feature toggle handling for conditional resource wiring
/// </summary>
internal static partial class Program
{
    /// <summary>
    /// Applies Application Insights references to all projects that support it
    /// </summary>
    /// <param name="projects">Project resources</param>
    /// <param name="insights">Application Insights resource (optional)</param>
    internal static void ApplyInsightsReferences(
        ProjectResources projects,
        IResourceBuilder<IResourceWithConnectionString>? insights)
    {
        if (insights is not null)
        {
            projects.ApiMain.WithReference(insights);
            projects.Silo.WithReference(insights);
            projects.McpServer.WithReference(insights);
            projects.DocGenFrontend.WithReference(insights);
        }
    }
}
