using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.AppHost;

/// <summary>
/// ApplicationBuilder extensions to support the Greenlight.AppHost
/// </summary>
public static class ProjectExtensions
{
    /// <summary>
    /// Adds an <see cref="IConfigurationSection"/> to the <see cref="ProjectResource"/>
    /// <see cref="IResourceBuilder{T}"/>.
    /// </summary>
    /// <param name="project">
    /// The <see cref="IResourceBuilder{T}"/> to add the <see cref="IConfigurationSection"/> to.
    /// </param>
    /// <param name="configSection">The <see cref="IConfigurationSection"/> to add.</param>
    /// <returns>
    /// The <see cref="IResourceBuilder{T}"/> with the added <see cref="IConfigurationSection"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="project"/> is null.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="configSection"/> is null.</exception>
    public static IResourceBuilder<ProjectResource> WithConfigSection(
        this IResourceBuilder<ProjectResource> project, IConfigurationSection configSection)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(configSection);

        BindSection(project, configSection, parentKey: "");

        return project;
    }

    private static void BindSection
    (
        IResourceBuilder<ProjectResource> project, IConfigurationSection section, string parentKey
    )
    {
        // If the current section is a leaf (has a value), add it with the full key
        if (section.Value != null)
        {
            string fullKey = CreateFullKey(parentKey, section.Key);
            project.WithEnvironment(fullKey, section.Value);
        }
        else
        {
            // For non-leaf sections, append the current section's key to the parent key
            string newParentKey = CreateFullKey(parentKey, section.Key);

            // Recursively process child sections
            foreach (var child in section.GetChildren())
            {
                BindSection(project, child, newParentKey);
            }
        }
    }

    private static string CreateFullKey(string parentKey, string childKey)
    {
        return string.IsNullOrEmpty(parentKey) ? childKey : $"{parentKey}__{childKey}";
    }

    /// <summary>
    /// Configures environment variables for proper Unicode/locale support in containers
    /// </summary>
    /// <typeparam name="T">The resource type</typeparam>
    /// <param name="builder">The resource builder</param>
    /// <returns>The resource builder for chaining</returns>
    internal static IResourceBuilder<T> WithUnicodeSupport<T>(this IResourceBuilder<T> builder)
        where T : class, IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("LANG", "en_US.UTF-8")
            .WithEnvironment("LANGUAGE", "en_US:en")
            .WithEnvironment("LC_ALL", "en_US.UTF-8")
            .WithEnvironment("LC_CTYPE", "en_US.UTF-8");
    }

    /// <summary>
    /// Configures .NET-specific environment variables for container environments
    /// </summary>
    /// <typeparam name="T">The resource type</typeparam>
    /// <param name="builder">The resource builder</param>
    /// <returns>The resource builder for chaining</returns>
    internal static IResourceBuilder<T> WithDotNetContainerDefaults<T>(this IResourceBuilder<T> builder)
        where T : class, IResourceWithEnvironment
    {
        return builder
            .WithEnvironment("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "false")
            .WithEnvironment("DOTNET_RUNNING_IN_CONTAINER", "true")
            .WithUnicodeSupport();
    }
}


