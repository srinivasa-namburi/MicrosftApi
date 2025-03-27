using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Extensions.Plugins.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
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

            string[] pluginAssemblyPaths = Directory.GetFiles(baseDirectory, "Microsoft.Greenlight.Plugins.*.dll")
                .Where(path => !path.Contains("Microsoft.Greenlight.Plugins.Shared"))
                .ToArray();

            string[] documentProcessAssemblyPaths = Directory.GetFiles(baseDirectory, "Microsoft.Greenlight.DocumentProcess.*.dll")
                .ToArray();

            var configuredDocumentProcesses = options.GreenlightServices.DocumentProcesses.Select(dp => dp!.Name).ToList();

            documentProcessAssemblyPaths = documentProcessAssemblyPaths
                .Where(path => configuredDocumentProcesses.Any(dp => path.Contains(dp)))
                .Concat(new[] { Path.Combine(baseDirectory, "Microsoft.Greenlight.DocumentProcess.Shared.dll") })
                .ToArray();

            // Combine all assembly paths and load them.
            var allAssemblyPaths = pluginAssemblyPaths.Concat(documentProcessAssemblyPaths).Distinct().ToArray();
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
        /// Adds shared and document process plugins to the kernel plugin collection.
        /// </summary>
        /// <param name="kernelPlugins">The kernel plugin collection.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="documentProcessOptions">The document process options.</param>
        /// <param name="excludedPluginTypes">An optional list of plugin types to exclude.</param>
        public static void AddSharedAndDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessOptions documentProcessOptions,
            List<Type>? excludedPluginTypes = null)
        {
            AddSharedAndStaticDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcessOptions.Name, excludedPluginTypes);
        }

        /// <summary>
        /// Adds shared and document process plugins to the kernel plugin collection.
        /// </summary>
        /// <param name="kernelPlugins">The kernel plugin collection.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="documentProcess">The document process information.</param>
        /// <param name="excludedPluginTypes">An optional list of plugin types to exclude.</param>
        public static void AddSharedAndDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes = null
           )
        {
            if (documentProcess.Source == ProcessSource.Static)
            {
                AddSharedAndStaticDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcess.ShortName, excludedPluginTypes);
            }
            else
            {
                // For dynamic plugins in a sync context, we'll have to use a blocking call
                // This is not ideal and should be used cautiously
                AddSharedAndDynamicDocumentProcessPluginsToPluginCollectionAsync(kernelPlugins, serviceProvider, documentProcess, excludedPluginTypes)
                    .GetAwaiter().GetResult();
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
            if (documentProcess.Source == ProcessSource.Static)
            {
                AddSharedAndStaticDocumentProcessPluginsToPluginCollection(kernelPlugins, serviceProvider, documentProcess.ShortName, excludedPluginTypes);
            }
            else
            {
                await AddSharedAndDynamicDocumentProcessPluginsToPluginCollectionAsync(kernelPlugins, serviceProvider, documentProcess, excludedPluginTypes);
            }
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
            var dynamicPluginManager = serviceProvider.GetRequiredService<DynamicPluginManager>();
            var assignedDynamicPlugins = new List<PluginRegistryEntry>();
            if (dynamicPlugins.Any())
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

        

        private static void AddSharedAndStaticDocumentProcessPluginsToPluginCollection(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            string documentProcessName,
            List<Type>? excludedPluginTypes = null)
        {
            AddSharedPluginsToPluginCollection(kernelPlugins, serviceProvider, excludedPluginTypes);
            var documentProcessPlugins = GetPluginsByAssemblyPrefix("Microsoft.Greenlight.DocumentProcess." + documentProcessName);

            // We also need to look in the shared plugins because some plugins are scoped to document processes but stored in the Shared assembly
            var sharedDocumentProcessPlugins = GetPluginsByAssemblyPrefix("Microsoft.Greenlight.DocumentProcess.Shared");
            documentProcessPlugins = documentProcessPlugins.Concat(sharedDocumentProcessPlugins).ToList();

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
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }
        }

        private static void AddSharedPluginsToPluginCollection(
            KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            List<Type>? excludedPluginTypes)
        {
            var basePlugins = GetPluginsByAssemblyPrefix("Microsoft.Greenlight.Plugins");
            var sharedDocumentProcessPlugins = GetPluginsByAssemblyPrefix("Microsoft.Greenlight.DocumentProcess.Shared");

            var allPlugins = basePlugins.Concat(sharedDocumentProcessPlugins).ToList();

            foreach (var pluginType in allPlugins)
            {
                if (excludedPluginTypes != null && excludedPluginTypes.Contains(pluginType))
                {
                    continue;
                }

                try
                {
                    // Only gets non-document process scoped plugins
                    var pluginInstance = serviceProvider.GetService(pluginType);
                    if (pluginInstance != null)
                    {
                        kernelPlugins.AddFromObject(pluginInstance, "native_" + pluginType.Name);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
                }
            }
        }

        private static List<Type> GetPluginsByAssemblyPrefix(string assemblyPrefix)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.FullName is not null && x.FullName.StartsWith(assemblyPrefix))
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IPluginImplementation).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .ToList();
        }
    }
}