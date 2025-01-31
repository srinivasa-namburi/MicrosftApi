using System.Reflection;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins;

/// <summary>
/// Represents information about a dynamically loaded plugin.
/// </summary>
public class LoadedDynamicPluginInfo
{
    /// <summary>
    /// Gets or sets the dynamic plugin.
    /// </summary>
    public DynamicPlugin Plugin { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version of the dynamic plugin.
    /// </summary>
    public DynamicPluginVersion Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the assembly containing the plugin.
    /// </summary>
    public Assembly Assembly { get; set; } = null!;

    /// <summary>
    /// Gets or sets the list of plugin types.
    /// </summary>
    public List<Type> PluginTypes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the temporary directory used for the plugin.
    /// </summary>
    public string TempDirectory { get; set; } = null!;

    /// <summary>
    /// Creates an instance of <see cref="LoadedDynamicPluginInfo"/> from the specified parameters.
    /// </summary>
    /// <param name="plugin">The dynamic plugin.</param>
    /// <param name="version">The version of the dynamic plugin.</param>
    /// <param name="tempDirectory">The temporary directory used for the plugin.</param>
    /// <param name="assembly">The assembly containing the plugin.</param>
    /// <returns>A new instance of <see cref="LoadedDynamicPluginInfo"/>.</returns>
    public static LoadedDynamicPluginInfo CreateFrom(DynamicPlugin plugin, DynamicPluginVersion version, string tempDirectory, Assembly assembly)
    {
        // We search by name instead of Type first since they're in fact different types when we load them in different contexts
        const string interfaceFullName = "Microsoft.Greenlight.Extensions.Plugins.IPluginImplementation";

        var pluginTypes = new List<Type>();

        // First attempt: Use interface name
        pluginTypes.AddRange(assembly.GetExportedTypes()
            .Where(t => t.GetInterfaces().Any(i => i.FullName == interfaceFullName) &&
                        t is { IsAbstract: false, IsInterface: false, IsClass: true }));

        // Second attempt: Use Type.GetType if first attempt yields no results
        if (pluginTypes.Count == 0)
        {
            var interfaceType = Type.GetType(interfaceFullName);
            if (interfaceType != null)
            {
                pluginTypes.AddRange(assembly.GetExportedTypes()
                    .Where(t => interfaceType.IsAssignableFrom(t) &&
                                t is { IsAbstract: false, IsInterface: false, IsClass: true }));
            }
        }

        Console.WriteLine($"Found {pluginTypes.Count} types implementing {interfaceFullName} in assembly: {assembly.FullName}");
        foreach (var type in pluginTypes)
        {
            Console.WriteLine($"  {type.FullName}");
        }

        return new LoadedDynamicPluginInfo
        {
            Plugin = plugin,
            Version = version,
            Assembly = assembly,
            PluginTypes = pluginTypes,
            TempDirectory = tempDirectory
        };
    }
}
