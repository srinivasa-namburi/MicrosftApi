using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.AppHost;

public static class ProjectExtensions
{
    public static IResourceBuilder<ProjectResource> WithConfigSection(this IResourceBuilder<ProjectResource> project, IConfigurationSection configSection)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (configSection == null) throw new ArgumentNullException(nameof(configSection));

        BindSection(project, configSection, parentKey: "");

        return project;
    }

    private static void BindSection(IResourceBuilder<ProjectResource> project, IConfigurationSection section, string parentKey)
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
}


