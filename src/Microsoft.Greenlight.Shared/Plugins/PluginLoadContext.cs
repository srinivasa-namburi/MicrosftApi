using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Greenlight.Shared.Plugins
{
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string _pluginPath;
        private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
        private readonly HashSet<string> _defaultContextAssemblies;

        public PluginLoadContext(string pluginPath, IEnumerable<string>? defaultContextAssemblies = null)
        {
            _pluginPath = pluginPath;
            _defaultContextAssemblies = new HashSet<string>(defaultContextAssemblies ?? [], StringComparer.OrdinalIgnoreCase) { "Microsoft.Extensions.Hosting" };
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (_loadedAssemblies.TryGetValue(assemblyName.FullName, out var cachedAssembly))
            {
                return cachedAssembly;
            }

            var resolvedAssembly = ResolveAndLoadAssembly(assemblyName);
            if (resolvedAssembly != null)
            {
                _loadedAssemblies[assemblyName.FullName] = resolvedAssembly;
            }
            return resolvedAssembly;
        }

        private Assembly? ResolveAndLoadAssembly(AssemblyName assemblyName)
        {
            Assembly? defaultAssembly = null;
            Version? defaultAssemblyVersion = null;
            Version? pluginAssemblyVersion = null;

            // Step 1: Check if the assembly is in the _defaultContextAssemblies collection
            if (_defaultContextAssemblies.Contains(assemblyName.Name))
            {
                // Always load from the Default context if it's in the _defaultContextAssemblies
                try
                {
                    defaultAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                    return defaultAssembly; // Return immediately if found in Default context
                }
                catch (FileNotFoundException)
                {
                    // If not found, fallback to checking loaded assemblies
                }

                // If LoadFromAssemblyName fails, check loaded assemblies in the default context
                defaultAssembly = AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);

                if (defaultAssembly != null)
                {
                    return defaultAssembly; // Return immediately if already loaded in Default context
                }
            }

            // Step 2: Try loading the assembly from the Default context (if not in _defaultContextAssemblies)
            try
            {
                defaultAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
                defaultAssemblyVersion = defaultAssembly?.GetName().Version;
            }
            catch (FileNotFoundException)
            {
                // Ignore and continue, since we'll also check in the plugin context
            }

            // If LoadFromAssemblyName fails, check loaded assemblies in the default context
            if (defaultAssembly == null)
            {
                defaultAssembly = AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
                defaultAssemblyVersion = defaultAssembly?.GetName().Version;
            }

            // Step 3: Try to locate the assembly in the plugin directory
            var pluginAssemblyPath = Path.Combine(_pluginPath, $"{assemblyName.Name}.dll");

            if (File.Exists(pluginAssemblyPath))
            {
                // Get the version of the plugin's assembly without loading it
                var pluginAssemblyName = AssemblyName.GetAssemblyName(pluginAssemblyPath);
                pluginAssemblyVersion = pluginAssemblyName.Version;

                // Compare versions and determine which assembly to load
                if (defaultAssemblyVersion != null && pluginAssemblyVersion != null)
                {
                    if (pluginAssemblyVersion > defaultAssemblyVersion)
                    {
                        return LoadFromAssemblyPath(pluginAssemblyPath); // Plugin version is newer
                    }
                    else
                    {
                        return defaultAssembly; // Default context version is newer or the same
                    }
                }

                // If no default assembly version or plugin version is missing, load from plugin path
                return LoadFromAssemblyPath(pluginAssemblyPath);
            }

            // Step 4: If no plugin assembly is found, return the assembly from the Default context (if it exists)
            return defaultAssembly;
        }



        public new void Unload()
        {
            _loadedAssemblies.Clear();
            base.Unload();
        }
    }
}