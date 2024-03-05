using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace ProjectVico.V2.Plugins.Shared
{
    public static class HostApplicationBuilderExtensions
    {
        public static IHostApplicationBuilder DynamicallyRegisterPlugins(this IHostApplicationBuilder builder)
        {
            // Define the base directory - assuming it's the current directory for simplicity
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Filter for assemblies starting with 'ProjectVico.V2.Plugins.' but not the shared one
            string[] assemblyPaths = Directory.GetFiles(baseDirectory, "ProjectVico.V2.Plugins.*.dll")
                .Where(path => !path.Contains("ProjectVico.V2.Plugins.Shared"))
                .ToArray();

            foreach (var path in assemblyPaths)
            {
                try
                {
                    // Load the assembly
                    var assembly = Assembly.LoadFrom(path);

                    // Check and register plugins from the assembly
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPluginRegistration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        .ToList();

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IPluginRegistration pluginInstance)
                        {
                            builder = pluginInstance.RegisterPlugin(builder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle or log exceptions as appropriate
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }

            return builder;
        }

        /// <summary>
        /// Finds all implementations of IPluginImplementation in the service provider and adds them to the kernel plugin collection
        /// </summary>
        /// <param name="kernelPlugins">This is the KernelPluginCollection being operated on</param>
        /// <param name="serviceProvider">Service Provider to search for Plugins</param>
        /// <param name="excludePluginType">Exclude the Plugin type in this list</param>
        public static void AddRegisteredPluginsToKernelPluginCollection(
            this KernelPluginCollection kernelPlugins, 
            IServiceProvider serviceProvider, 
            Type excludePluginType = null)
        {
            // Load all types that implement IPluginImplementation from all assemblies
            // You might want to restrict the search to certain assemblies for performance reasons
            var pluginTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x=>x.FullName.StartsWith("ProjectVico.V2.Plugins"))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                // Ensure the type is not the one to exclude
                if (excludePluginType == null || pluginType != excludePluginType)
                {
                    // Attempt to resolve the type from the service provider
                    var pluginInstance = serviceProvider.GetService(pluginType);
                    if (pluginInstance != null)
                    {
                        // Add the plugin to the collection, excluding the specified type
                        kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                    }
                }
            }
        }


    }
}