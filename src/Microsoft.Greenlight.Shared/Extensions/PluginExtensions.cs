using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Extensions.Plugins.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Services;
using ModelContextProtocol.Client;
using System.Text.RegularExpressions;

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

            // Register the plugin initializer hosted service
            builder.Services.AddHostedService<PluginInitializerHostedService>();
            
            // Register MCP plugin services - this now uses the AddMcpPluginServices extension method
            builder.AddMcpPluginServices();

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
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task AddSharedAndDocumentProcessPluginsToPluginCollectionAsync(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes = null
            )
        {
            // Add standard plugins
            await AddSharedAndStaticPluginsToPluginCollectionAsync(kernelPlugins, serviceProvider, documentProcess, excludedPluginTypes);
            
            // Add MCP plugins for the document process
            await AddMcpPluginsToPluginCollectionAsync(kernelPlugins, serviceProvider, documentProcess);
        }

        private static async Task AddSharedAndStaticPluginsToPluginCollectionAsync(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess,
            List<Type>? excludedPluginTypes)
        {
            var pluginRegistry = serviceProvider.GetRequiredService<IPluginRegistry>();

            // Get all plugins from the registry
            var allPlugins = pluginRegistry.AllPlugins;

            // We only care about shared/static plugins now, not dynamic plugins
            var sharedStaticPlugins = allPlugins.Where(p => !p.IsDynamic).ToList();

            foreach (var pluginEntry in sharedStaticPlugins)
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

                    if (string.IsNullOrEmpty(pluginRegistrationKey))
                    {
                        // Use a more reliable key
                        pluginRegistrationKey = pluginEntry.PluginInstance.GetType().ShortDisplayName();
                    }

                    kernelPlugins.AddFromObject(pluginEntry.PluginInstance, pluginRegistrationKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error loading or registering plugin {pluginEntry.PluginInstance.GetType().FullName}: {ex.Message}");
                }
            }
            
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Adds MCP plugins to the kernel plugin collection asynchronously.
        /// </summary>
        /// <param name="kernelPlugins">The kernel plugin collection.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task AddMcpPluginsToPluginCollectionAsync(
            this KernelPluginCollection kernelPlugins,
            IServiceProvider serviceProvider,
            DocumentProcessInfo documentProcess)
        {
            // Get the MCP plugin manager
            var mcpPluginManager = serviceProvider.GetService<McpPluginManager>();
            if (mcpPluginManager == null)
            {
                // MCP plugin manager is not available
                return;
            }
            
            var logger = serviceProvider.GetService<ILogger<McpPluginManager>>();
            
            try
            {
                // Ensure MCP plugins are loaded
                await mcpPluginManager.EnsurePluginsLoadedAsync();
                
                // Get MCP plugins for the document process
                var mcpPlugins = await mcpPluginManager.GetPluginsForDocumentProcessAsync(documentProcess);
                if (!mcpPlugins.Any())
                {
                    logger?.LogDebug("No MCP plugins found for document process: {ProcessName}", documentProcess.ShortName);
                    return;
                }
                
                logger?.LogInformation("Found {Count} MCP plugins for document process {ProcessName}", 
                    mcpPlugins.Count(), documentProcess.ShortName);
                
                int addedPluginCount = 0;
                
                // Process each plugin
                foreach (var plugin in mcpPlugins)
                {
                    try
                    {
                        // Get kernel functions directly from the plugin
                        var kernelFunctions = await plugin.GetKernelFunctionsAsync(documentProcess);
                        
                        if (kernelFunctions.Any())
                        {
                            // Construct a plugin name that includes the document process short name for proper context

                            // Use a regex replace to remove any invalid characters from the plugin name. Turn them into underscores.
                            // The plugin name can only contain letters, numbers, and underscores.
                            var sanitizedPluginName = Regex.Replace(plugin.Name, @"[^a-zA-Z0-9_]", "_");
                            var pluginName = $"mcp_{sanitizedPluginName}";
                            
                            // Add the functions to the kernel plugin collection
                            kernelPlugins.AddFromFunctions(pluginName, kernelFunctions);
                            addedPluginCount++;
                            
                            logger?.LogInformation("Added MCP plugin {PluginName} with {FunctionCount} functions to kernel", 
                                pluginName, kernelFunctions.Count);
                        }
                        else
                        {
                            logger?.LogWarning("MCP plugin {PluginName} returned no kernel functions", plugin.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error adding MCP plugin {PluginName} to kernel", plugin.Name);
                    }
                }
                
                logger?.LogInformation("Added {Count} MCP plugins to the kernel for document process {ProcessName}", 
                    addedPluginCount, documentProcess.ShortName);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error adding MCP plugins to the kernel for document process {ProcessName}", 
                    documentProcess.ShortName);
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
