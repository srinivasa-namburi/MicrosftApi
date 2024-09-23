using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Shared.Extensions
{
    public static class HostApplicationBuilderExtensions
    {
        public static IHostApplicationBuilder DynamicallyRegisterPlugins(this IHostApplicationBuilder builder, ServiceConfigurationOptions options)
        {

            // Define the base directory - assuming it's the current directory for simplicity
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Filter for assemblies starting with 'ProjectVico.V2.Plugins.' but not the shared one
            string[] pluginAssemblyPaths = Directory.GetFiles(baseDirectory, "ProjectVico.V2.Plugins.*.dll")
                .Where(path => !path.Contains("ProjectVico.V2.Plugins.Shared"))
                .ToArray();
            
            // Filter for assemblies starting with 'ProjectVico.V2.DocumentProcess.' including the shared one
            string [] documentProcessAssemblyPaths = Directory.GetFiles(baseDirectory, "ProjectVico.V2.DocumentProcess.*.dll")
                .ToArray();

            // We only want to load Document Process assemblies that match Document Processes that are loaded in our configuration.
            // This is to prevent loading assemblies that are not needed.
            var configuredDocumentProcesses = options.ProjectVicoServices.DocumentProcesses.Select(documentProcess => documentProcess.Name).ToList();
            
            // Get the paths of the assemblies in the configured Document Processes, and also register plugins from the ProjectVico.V2.DocumentProcess.Shared assembly
            documentProcessAssemblyPaths = documentProcessAssemblyPaths
                .Where(path => configuredDocumentProcesses.Any(documentProcess => path.Contains(documentProcess)))
                .Concat(new[] { Path.Combine(baseDirectory, "ProjectVico.V2.DocumentProcess.Shared.dll") })
                .ToArray();
            

            builder.RegisterPluginsForAssemblies(pluginAssemblyPaths);
            builder.RegisterPluginsForAssemblies(documentProcessAssemblyPaths);

            return builder;
        }

        private static IHostApplicationBuilder RegisterPluginsForAssemblies(this IHostApplicationBuilder builder,
            IEnumerable<string> assemblyPaths)
        {
            foreach (var path in assemblyPaths)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);

                    // Check and register plugins from the assembly
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPluginRegistration).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
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
                .Where(x=>
                    x.FullName.StartsWith("ProjectVico.V2.Plugins") || 
                    x.FullName.StartsWith("ProjectVico.V2.DocumentProcess"))
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

        public static void AddSharedAndDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessOptions documentProcess,
            List<Type>? excludedPluginTypes = null)
        {
            AddSharedAndStaticDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcess.Name, excludedPluginTypes);
        }

        public static void AddSharedAndDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes = null)
        {
            if (documentProcess.Source == ProcessSource.Static)
            {
                AddSharedAndStaticDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcess.ShortName, excludedPluginTypes);
            }
            else
            {
                AddSharedAndDynamicDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcess, excludedPluginTypes);
            }
            
        }

        private static void AddSharedAndDynamicDocumentProcessPluginsToPluginCollection(KernelPluginCollection kernelPlugins, IServiceProvider serviceProvider, DocumentProcessInfo documentProcess, List<Type> excludedPluginTypes)
        {
            AddSharedPluginsToPluginCollection(kernelPlugins, serviceProvider, excludedPluginTypes);

            var documentProcessPlugins = GetPluginsByAssemblyPrefix("ProjectVico.V2.DocumentProcess.Shared");

            foreach (var pluginType in documentProcessPlugins)
            {
                if (excludedPluginTypes != null && excludedPluginTypes.Contains(pluginType))
                {
                    continue;
                }

                try
                {
                    var pluginInstance =
                        serviceProvider.GetService(pluginType) ??
                        serviceProvider.GetRequiredKeyedService(pluginType,
                            documentProcess.ShortName + "-" + pluginType.Name);

                    kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                }
                catch (Exception ex)
                {
                    // Handle or log exceptions as appropriate
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }
        }

        private static void AddSharedAndStaticDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            string documentProcessName,
            List<Type>? excludedPluginTypes = null)
        {
            AddSharedPluginsToPluginCollection(kernelPlugins, serviceProvider, excludedPluginTypes);
            var documentProcessPlugins = GetPluginsByAssemblyPrefix("ProjectVico.V2.DocumentProcess." + documentProcessName);
            
            foreach (var pluginType in documentProcessPlugins)
            {
                if (excludedPluginTypes != null && excludedPluginTypes.Contains(pluginType))
                {
                    continue;
                }

                try
                {
                    var pluginInstance =
                        serviceProvider.GetService(pluginType) ??
                        serviceProvider.GetRequiredKeyedService(pluginType,
                            documentProcessName + "-" + pluginType.Name);

                    kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                }
                catch (Exception ex)
                {
                    // Handle or log exceptions as appropriate
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }
        }

        private static void AddSharedPluginsToPluginCollection(KernelPluginCollection kernelPlugins, IServiceProvider serviceProvider, List<Type> excludedPluginTypes)
        {
            var basePlugins = GetPluginsByAssemblyPrefix("ProjectVico.V2.Plugins");
            var sharedDocumentProcessPlugins = GetPluginsByAssemblyPrefix("ProjectVico.V2.DocumentProcess.Shared");
            
            var allPlugins = basePlugins.Concat(sharedDocumentProcessPlugins).ToList();

            foreach (var pluginType in allPlugins)
            {
                if (excludedPluginTypes != null && excludedPluginTypes.Contains(pluginType))
                {
                    continue;
                }

                try
                {
                    var pluginInstance =
                        serviceProvider.GetService(pluginType);
                      
                    kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                }
                catch (Exception ex)
                {
                    // Handle or log exceptions as appropriate
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }
        }

        private static List<Type> GetPluginsByAssemblyPrefix(string assemblyPrefix)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.FullName.StartsWith(assemblyPrefix))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();
        }
    }
}