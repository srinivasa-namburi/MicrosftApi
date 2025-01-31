using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using AutoMapper;
using Microsoft.Greenlight.Extensions.Plugins;

namespace Microsoft.Greenlight.Shared.Plugins
{
    /// <summary>
    /// Manages the dynamic loading and unloading of plugins.
    /// </summary>
    public class DynamicPluginManager
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private bool _pluginsLoaded;
        private readonly object _lockObject = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPluginManager"/> class.
        /// </summary>
        /// <param name="serviceScopeFactory">The service scope factory.</param>
        /// <param name="pluginContainer">The dynamic plugin container.</param>
        public DynamicPluginManager(
            IServiceScopeFactory serviceScopeFactory,
            DynamicPluginContainer pluginContainer)
        {
            _serviceScopeFactory = serviceScopeFactory;
            PluginContainer = pluginContainer;
        }

        /// <summary>
        /// Gets the dynamic plugin container.
        /// </summary>
        public DynamicPluginContainer PluginContainer { get; }

        /// <summary>
        /// Ensures that plugins are loaded.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task EnsurePluginsLoadedAsync(IServiceCollection services)
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously       
        {
            if (!_pluginsLoaded)
            {
                lock (_lockObject)
                {
                    if (!_pluginsLoaded) // Double-check locking
                    {
                        LoadDynamicPluginsAsync(services).GetAwaiter().GetResult();
                        _pluginsLoaded = true;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the plugin information for a specific type asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <param name="pluginType">The plugin type.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the loaded dynamic plugin information.</returns>
        public async Task<LoadedDynamicPluginInfo?> GetPluginInfoForTypeAsync(DocumentProcessInfo documentProcess, Type pluginType)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

            var dynamicDocumentProcessModel = await dbContext.DynamicDocumentProcessDefinitions
                .Include(dp => dp.Plugins)!
                .ThenInclude(p => p.DynamicPlugin)
                .AsNoTracking()
                .FirstOrDefaultAsync(dp => dp.Id == documentProcess.Id);

            if (dynamicDocumentProcessModel?.Plugins == null)
            {
                return null;
            }

            foreach (var pluginAssociation in dynamicDocumentProcessModel.Plugins)
            {
                var plugin = pluginAssociation.DynamicPlugin;
                var versionToUse = pluginAssociation.Version ?? plugin!.LatestVersion;
                if (PluginContainer.TryGetPlugin(plugin!.Name, versionToUse!.ToString(), out var pluginInfo) &&
                    pluginInfo!.PluginTypes.Contains(pluginType))
                {
                    return pluginInfo;
                }
            }

            return null;
        }

        private async Task LoadDynamicPluginsAsync(IServiceCollection services)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
            var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
            var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

            List<DynamicPlugin> dynamicPlugins;
            try
            {
                dynamicPlugins = await dbContext.DynamicPlugins
                    .Include(dp => dp.DocumentProcesses)
                    .ThenInclude(dp => dp.DynamicDocumentProcessDefinition)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to load dynamic plugins yet: {ex.Message}");
                return;
            }

            foreach (var plugin in dynamicPlugins)
            {
                foreach (var version in plugin.Versions)
                {
                    var pluginStream = await azureFileHelper.GetFileAsStreamFromContainerAndBlobName(
                        plugin.BlobContainerName, plugin.GetBlobName(version));

                    if (pluginStream == null)
                    {
                        Console.WriteLine($"Failed to download plugin: {plugin.Name}, version: {version}");
                        continue;
                    }

                    var pluginDirectory = GetPluginDownloadDirectory(plugin, version);
                    Directory.CreateDirectory(pluginDirectory);

                    try
                    {
                        // Clean the directory
                        foreach (var file in Directory.GetFiles(pluginDirectory, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (IOException exception)
                            {
                                Console.WriteLine($"Error deleting file {file}: {exception.Message} - skipping");
                            }
                        }

                        UnzipPlugin(pluginStream, pluginDirectory);
                        var mainAssemblyPath = FindMainAssemblyPath(pluginDirectory);

                        if (mainAssemblyPath != null)
                        {
                            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(mainAssemblyPath);

                            // Load all DLLs in the plugin directory to resolve dependencies
                            foreach (var dllPath in Directory.GetFiles(pluginDirectory, "*.dll"))
                            {
                                if (dllPath != mainAssemblyPath)
                                {
                                    var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                                    if (AssemblyLoadContext.Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name &&
                                            a.GetName().Version >= assemblyName.Version))
                                    {
                                        continue;
                                    }
                                    AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
                                }
                            }

                            RegisterPluginTypes(assembly, scope.ServiceProvider, services);
                            var pluginInfo = CreatePluginInfo(assembly, plugin, version, pluginDirectory);
                            PluginContainer.AddPlugin(plugin.Name, version.ToString(), pluginInfo);
                        }
                        else
                        {
                            Console.WriteLine($"Main assembly not found for plugin: {plugin.Name}, version: {version}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading plugin {plugin.Name}, version {version}: {ex.Message}");
                        Console.WriteLine($"StackTrace: {ex.StackTrace}");
                    }
                    finally
                    {
                        await pluginStream.DisposeAsync();
                    }
                }
            }

            _pluginsLoaded = true;
        }

        private static string GetPluginDownloadDirectory(DynamicPlugin plugin, DynamicPluginVersion version)
        {
            // This is a temporary directory where we extract the plugin zip file
            // It needs to be unique for each appdomain, instance, hostname, etc.

            var directoryElements = new List<string>
                {
                    "greenlight-plugins",
                    Environment.MachineName,
                    AppDomain.CurrentDomain.FriendlyName,
                    "process-" + Environment.ProcessId.ToString(),
                    plugin.Name,
                    version.ToString()
                };

            var pluginDirectory = Path.Combine(Path.GetTempPath(), Path.Combine(directoryElements.ToArray()));
            return pluginDirectory;
        }

        private static void RegisterPluginTypes(Assembly assembly, IServiceProvider sp, IServiceCollection services)
        {
            const string registrationInterfaceFullName = "Microsoft.Greenlight.Extensions.Plugins.IPluginRegistration";
            var registrationTypes = GetImplementingTypes(assembly, registrationInterfaceFullName);

            foreach (var type in registrationTypes)
            {
                if (Activator.CreateInstance(type) is IPluginRegistration pluginInstance)
                {
                    pluginInstance.RegisterPlugin(services, sp);
                }
            }
        }

        private static LoadedDynamicPluginInfo CreatePluginInfo(Assembly assembly, DynamicPlugin plugin, DynamicPluginVersion version, string tempDirectory)
        {
            const string interfaceFullName = "Microsoft.Greenlight.Extensions.Plugins.IPluginImplementation";
            var pluginTypes = GetImplementingTypes(assembly, interfaceFullName);

            if (pluginTypes.Count == 0)
            {
                Console.WriteLine($"Warning: No plugin types found for {plugin.Name}, version: {version}");
                Console.WriteLine($"Assembly: {assembly.FullName}");
                Console.WriteLine($"Exported Types:");
                foreach (var type in assembly.GetExportedTypes())
                {
                    Console.WriteLine($"  {type.FullName}");
                }
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

        /// <summary>
        /// Gets the plugin types for a specific document process asynchronously.
        /// </summary>
        /// <param name="documentProcess">The document process information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the plugin types.</returns>
        public async Task<IEnumerable<Type>> GetPluginTypesAsync(DocumentProcessInfo documentProcess)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

            var dynamicDocumentProcessModel = await dbContext.DynamicDocumentProcessDefinitions
                .Include(dp => dp.Plugins)!
                .ThenInclude(p => p.DynamicPlugin)
                .AsNoTracking()
                .FirstOrDefaultAsync(dp => dp.Id == documentProcess.Id);

            if (dynamicDocumentProcessModel == null)
            {
                return Enumerable.Empty<Type>();
            }

            var result = new List<Type>();

            foreach (var pluginAssociation in dynamicDocumentProcessModel.Plugins!)
            {
                var plugin = pluginAssociation.DynamicPlugin;
                var versionToUse = pluginAssociation.Version ?? plugin!.LatestVersion;
                if (PluginContainer.TryGetPlugin(plugin!.Name, versionToUse!.ToString(), out var pluginInfo))
                {
                    result.AddRange(pluginInfo!.PluginTypes);
                }
            }

            return result;
        }

        private static List<Type> GetImplementingTypes(Assembly assembly, string interfaceFullName)
        {
            var implementingTypes = new List<Type>();

            // First attempt: Use interface name
            implementingTypes.AddRange(assembly.GetExportedTypes()
                .Where(t => t.GetInterfaces().Any(i => i.FullName == interfaceFullName) &&
                            t is { IsAbstract: false, IsInterface: false, IsClass: true }));

            // Second attempt: Use Type.GetType if first attempt yields no results
            if (implementingTypes.Count == 0)
            {
                var interfaceType = Type.GetType(interfaceFullName);
                if (interfaceType != null)
                {
                    implementingTypes.AddRange(assembly.GetExportedTypes()
                        .Where(t => interfaceType.IsAssignableFrom(t) &&
                                    t is { IsAbstract: false, IsInterface: false, IsClass: true }));
                }
            }

            return implementingTypes;
        }

        private static void UnzipPlugin(Stream zipStream, string targetDirectory)
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            archive.ExtractToDirectory(targetDirectory);
        }

        private static string? FindMainAssemblyPath(string directory)
        {
            var depsFile = Directory.GetFiles(directory, "*.deps.json").FirstOrDefault();
            if (depsFile != null)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(depsFile).Replace(".deps", "");
                return Path.Combine(directory, $"{assemblyName}.dll");
            }
            return Directory.GetFiles(directory, "*.dll").FirstOrDefault();
        }
    }
}
