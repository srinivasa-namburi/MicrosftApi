using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Extensions.Plugins.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Shared.Extensions
{
    /// <summary>
    /// Provides extension methods for plugin registration and management.
    /// </summary>
    public static class PluginExtensions
    {
        /// <summary>
        /// Registers static plugins from the specified assemblies.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="options">The service configuration options.</param>
        /// <returns>The updated host application builder.</returns>
        public static IHostApplicationBuilder RegisterStaticPlugins(this IHostApplicationBuilder builder, ServiceConfigurationOptions options)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            string[] allAssemblyPaths = Directory.GetFiles(baseDirectory, "Microsoft.Greenlight.Plugins.*.dll")
                .Where(path => !path.Contains("Microsoft.Greenlight.Plugins.Shared"))
                .ToArray();

            // Combine all assembly paths and load them.
            var assemblies = allAssemblyPaths.Select(Assembly.LoadFrom).ToArray();

            // Use Scrutor assembly scanning to scan these assemblies for types implementing IPluginInitializer
            // These will all be added to the DI container as singleton for later execution using the PluginInitializerHostedService.

            builder.Services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo<IPluginInitializer>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime());

            // Register plugins from the specified assembly paths.
            builder.RegisterPluginsForAssemblies(allAssemblyPaths);

            // Optionally, if your plugins need to perform deferred initialization, register a hosted service.
            builder.Services.AddHostedService<PluginInitializerHostedService>();

            return builder;
        }


        /// <summary>
        /// Registers plugins from the specified assembly paths.
        /// </summary>
        /// <param name="builder">The host application builder.</param>
        /// <param name="assemblyPaths">The assembly paths for the plugins.</param>
        /// <returns>The updated host application builder.</returns>
        private static IHostApplicationBuilder RegisterPluginsForAssemblies(this IHostApplicationBuilder builder,
            IEnumerable<string> assemblyPaths)
        {

            foreach (var path in assemblyPaths)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);

                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPluginRegistration).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                        .ToList();

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is IPluginRegistration pluginInstance)
                        {
                            pluginInstance.RegisterPlugin(builder.Services);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }

            return builder;
        }

        /// <summary>
        /// Adds registered plugins to the kernel plugin collection.
        /// </summary>
        /// <param name="kernelPlugins">The kernel plugin collection.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="excludePluginType">Optional type of plugin to exclude.</param>
        public static void AddRegisteredPluginsToKernelPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            Type? excludePluginType = null)
        {
            var pluginTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x =>
                    x.FullName is not null &&
                    (x.FullName.StartsWith("Microsoft.Greenlight.Plugins") ||
                    x.FullName.StartsWith("Microsoft.Greenlight.DocumentProcess")))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();

            foreach (var pluginType in pluginTypes)
            {
                if (excludePluginType == null || pluginType != excludePluginType)
                {
                    var pluginInstance = serviceProvider.GetService(pluginType);
                    if (pluginInstance != null)
                    {
                        kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Adds shared and document process plugins to the kernel plugin collection asynchronously.
        /// </summary>
        /// <param name="kernelPlugins">The kernel plugin collection.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="documentProcess">The document process information.</param>
        /// <param name="excludedPluginTypes">An optional list of plugin types to exclude.</param>
        /// <returns></returns>
        public static async Task AddSharedAndDocumentProcessPluginsToPluginCollectionAsync(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes = null
            )
        {
            await AddSharedAndDynamicDocumentProcessPluginsToPluginCollectionAsync(kernelPlugins, serviceProvider, documentProcess, excludedPluginTypes);
        }

        private static async Task AddSharedAndDynamicDocumentProcessPluginsToPluginCollectionAsync(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes)
        {
            var pluginRegistry = serviceProvider.GetRequiredService<IPluginRegistry>();

            // Get all plugins from the registry
            var allPlugins = pluginRegistry.AllPlugins;

            // Separate shared/static plugins and dynamic plugins
            var sharedStaticPlugins = allPlugins.Where(p => !p.IsDynamic).ToList();
            var dynamicPlugins = allPlugins.Where(p => p.IsDynamic).ToList();

            // Filter dynamic plugins based on the document process
            var dynamicPluginManager = serviceProvider.GetService<DynamicPluginManager>();
            var assignedDynamicPlugins = new List<PluginRegistryEntry>();

            // Get the dynamic plugins that are assigned to the document process - if the 
            // dynamic plugin manager is available

            if (dynamicPlugins.Any() && dynamicPluginManager != null)
            {
                var dynamicPluginTypes = await dynamicPluginManager.GetPluginTypesAsync(documentProcess);
                assignedDynamicPlugins = dynamicPlugins
                    .Where(p => dynamicPluginTypes.Contains(p.PluginInstance.GetType()))
                    .ToList();
            }

            // Combine shared/static plugins and assigned dynamic plugins
            var combinedPlugins = sharedStaticPlugins.Concat(assignedDynamicPlugins);

            foreach (var pluginEntry in combinedPlugins)
            {
                if (excludedPluginTypes != null &&
                    excludedPluginTypes.Contains(pluginEntry.PluginInstance.GetType()))
                {
                    continue;
                }

                // Ensure only the relevant KmDocsPlugin for the specific DocumentProcess is added
                if (pluginEntry.Key.Contains("KmDocsPlugin"))
                {
                    // Get the document process name from the plugin key
                    var pluginDocumentProcessName = pluginEntry.Key.Split('-')[0];
                    if (pluginDocumentProcessName != documentProcess.ShortName)
                    {
                        continue;
                    }
                }

                try
                {
                    string? pluginRegistrationKey =
                        pluginEntry.PluginInstance.GetType().GetServiceKeyForPluginType();

                    if (sharedStaticPlugins.Contains(pluginEntry))
                    {
                        // This is a shared, static plugin and not a dynamic one
                        pluginRegistrationKey =
                            //documentProcess.ShortName.Replace(".", "_").Replace("-", "_").Replace(" ", "_") + "__" +
                            pluginEntry.PluginInstance.GetType().Name;
                    }

                    kernelPlugins.AddFromObject(pluginEntry.PluginInstance, pluginRegistrationKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error loading or registering plugin {pluginEntry.PluginInstance.GetType().FullName}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Removes main repository plugin from plugin collection as it may interfere with generation, where
        /// documents from the main repository are retrieved ahead of execution.
        /// </summary>
        /// <param name="kernel">The Semantic Kernel instance (must be instantiated).</param>
        /// <param name="documentProcessName">Document Process to remove KmDocs plugin for. We want to keep other instances.</param>
        public static void PrepareSemanticKernelInstanceForGeneration(this Kernel kernel, string documentProcessName)
        {
            var kmDocsPlugins = kernel.Plugins.Where(x => x.Name.Contains("KmDocsPlugin")).ToList();

            foreach (var kmDocsPlugin in kmDocsPlugins
                         .Where(kmDocsPlugin => kmDocsPlugin.Name.Contains(documentProcessName) ||
                                                kmDocsPlugin.Name.Contains("native") ||
                                                kmDocsPlugin.Name.ToLower() == "kmdocsplugin"))
            {
                kernel.Plugins.Remove(kmDocsPlugin);
            }
        }
    }
}