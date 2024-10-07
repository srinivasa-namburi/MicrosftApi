using System.Reflection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins;

public class DynamicPluginInfo
{
    public DynamicPlugin Plugin { get; set; }
    public DynamicPluginVersion Version { get; set; }
    public Assembly Assembly { get; set; }
    public List<Type> PluginTypes { get; set; }
    public PluginLoadContext LoadContext { get; set; }
    public string TempDirectory { get; set; }

    public static DynamicPluginInfo CreateFrom(DynamicPlugin plugin, DynamicPluginVersion version, string tempDirectory, Assembly assembly, PluginLoadContext loadContext)
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

        return new DynamicPluginInfo
        {
            Plugin = plugin,
            Version = version,
            Assembly = assembly,
            PluginTypes = pluginTypes,
            LoadContext = loadContext,
            TempDirectory = tempDirectory
        };
    }
}