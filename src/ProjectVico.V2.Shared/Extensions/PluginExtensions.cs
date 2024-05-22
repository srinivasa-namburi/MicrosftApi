using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.Plugins.Shared
{
    public static class HostApplicationBuilderExtensions
    {
        public static IHostApplicationBuilder DynamicallyRegisterPlugins(this IHostApplicationBuilder builder, ServiceConfigurationOptions options)
        {

            // Document Info Service and associated mappings
            builder.Services.AddAutoMapper(typeof(DocumentProcessInfoProfile));
            builder.Services.AddScoped<IDocumentProcessInfoService, DocumentProcessInfoService>();
            builder.Services.AddScoped<IPromptInfoService, PromptInfoService>();

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
            // Load all types that implement IPluginImplementation from Plugins assemblies.
            // Note that this scans for assemblies that have already been loaded - so RegisterPluginsForAssemblies should be called first
            
            var basePlugins = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x=>
                    x.FullName.StartsWith("ProjectVico.V2.Plugins"))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            var documentProcessPlugins = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x=> x.FullName.StartsWith("ProjectVico.V2.DocumentProcess."+documentProcess.Name))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            var sharedDocumentProcessPlugins = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.FullName.StartsWith("ProjectVico.V2.DocumentProcess.Shared"))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            documentProcessPlugins = documentProcessPlugins.Concat(sharedDocumentProcessPlugins).ToList();

            var allPlugins = basePlugins.Concat(documentProcessPlugins).ToList();

            foreach (var pluginType in allPlugins)
            {
                // Ensure the type is not on the exclusionList
                if (excludedPluginTypes == null || !excludedPluginTypes.Contains(pluginType))
                {
                    // Attempt to resolve the type from the service provider
                    var pluginInstance = serviceProvider.GetService(pluginType);

                    // If the plugin instance is null, we will try to resolve for a Keyed service of the same time prefixed with the document process name
                    if (pluginInstance == null)
                    {
                        try
                        {
                            pluginInstance = serviceProvider.GetRequiredKeyedService(pluginType,
                                documentProcess.Name + "-" + pluginType.Name);
                        }
                        catch
                        {
                            // Don't do anything if the service is not found
                        }
                    }

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