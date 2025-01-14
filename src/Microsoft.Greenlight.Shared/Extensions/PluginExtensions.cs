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

            var configuredDocumentProcesses = options.GreenlightServices.DocumentProcesses.Select(documentProcess => documentProcess!.Name).ToList();

            documentProcessAssemblyPaths = documentProcessAssemblyPaths
                .Where(path => configuredDocumentProcesses.Any(documentProcess => path.Contains(documentProcess)))
                .Concat(new[] { Path.Combine(baseDirectory, "Microsoft.Greenlight.DocumentProcess.Shared.dll") })
                .ToArray();

            builder.RegisterPluginsForAssemblies(pluginAssemblyPaths);
            builder.RegisterPluginsForAssemblies(documentProcessAssemblyPaths);

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
            // Build the Service Provider here
            var sp = builder.Services.BuildServiceProvider();

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
                            pluginInstance.RegisterPlugin(builder.Services, sp);
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
            AddSharedPluginsToPluginCollection(kernelPlugins, serviceProvider, excludedPluginTypes);

            var sharedStaticPlugins = GetPluginsByAssemblyPrefix("Microsoft.Greenlight.DocumentProcess.Shared")
                .ToList();

            // Only load dynamic plugins if we can find the DynamicPluginManager in the DI container
            var dynamicPlugins = new List<Type>();
            if (serviceProvider.GetServices<DynamicPluginManager>().Any())
            {
                var dynamicPluginManager = serviceProvider.GetRequiredService<DynamicPluginManager>();
                var dynamicPluginEnumerable = await dynamicPluginManager.GetPluginTypesAsync(documentProcess);
                dynamicPlugins = dynamicPluginEnumerable.ToList();
            }

            var allPlugins = sharedStaticPlugins.Concat(dynamicPlugins);

            foreach (var pluginType in allPlugins)
            {
                if (excludedPluginTypes != null && excludedPluginTypes.Contains(pluginType))
                {
                    continue;
                }

                try
                {
                    string? pluginRegistrationKey = pluginType.GetServiceKeyForPluginType();
                    object pluginInstance;
                    if (sharedStaticPlugins.Contains(pluginType))
                    {
                        //This is a shared, static plugin and not a dynamic one
                        try
                        {
                            // We try to resolve for DocumentProcessName-PluginType first
                            try
                            {
                                pluginInstance = serviceProvider.GetRequiredKeyedService(pluginType,
                                    documentProcess.ShortName + "-" + pluginType.Name);
                                pluginRegistrationKey = documentProcess.ShortName.Replace(".", "_").Replace("-","_").Replace(" ","_") + "__" + pluginType.Name;
                            }
                            catch
                            {
                                pluginInstance = serviceProvider.GetRequiredKeyedService(pluginType, pluginRegistrationKey);
                            }
                        }
                        catch
                        {
                            pluginInstance = serviceProvider.GetRequiredService(pluginType);
                        }

                        if (pluginInstance == null)
                        {
                            Console.WriteLine($"Not found in container : {pluginType.FullName}");
                            continue;
                        }

                    }
                    else
                    {
                        //This is a dynamic plugin
                        try
                        {
                            using var scope = serviceProvider.CreateScope();
                            var scopedProvider = scope.ServiceProvider;
                            pluginInstance = scopedProvider.GetRequiredKeyedService(pluginType, pluginRegistrationKey);

                        }
                        catch
                        {
                            Console.WriteLine($"Not found in container : {pluginType.FullName}");
                            continue;
                        }
                    }

                    kernelPlugins.AddFromObject(pluginInstance, pluginRegistrationKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading or registering plugin {pluginType.FullName}: {ex.Message}");
                }
            }
        }

        private static async Task<LoadedDynamicPluginInfo> GetDynamicPluginInfoAsync(
            DynamicPluginManager dynamicPluginManager,
            DocumentProcessInfo documentProcess,
            Type pluginType)
        {
            var pluginInfo = await dynamicPluginManager.GetPluginInfoForTypeAsync(documentProcess, pluginType);
            if (pluginInfo == null)
            {
                throw new InvalidOperationException($"Could not find plugin info for type {pluginType.FullName}");
            }
            return pluginInfo;
        }

        private static List<Type> GetImplementingTypes(Assembly assembly, string interfaceFullName)
        {
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

            return pluginTypes;
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