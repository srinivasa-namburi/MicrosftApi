using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public class DynamicPluginManager
    {
        private readonly IServiceProvider _serviceProvider;
        private IHostApplicationBuilder _builder;
        private bool _pluginsLoaded = false;
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, Dictionary<DynamicPluginVersion, DynamicPluginInfo>> _loadedPlugins = new();

        public DynamicPluginManager(
            IServiceProvider serviceProvider,
            IHostApplicationBuilder builder
            )
        {
            _serviceProvider = serviceProvider;
            _builder = builder;
            //EnsurePluginsLoadedAsync().GetAwaiter().GetResult();
        }

        public async Task EnsurePluginsLoadedAsync()
        {
            if (!_pluginsLoaded)
            {
                lock (_lockObject)
                {
                    if (!_pluginsLoaded)
                    {
                        LoadDynamicPluginsAsync().GetAwaiter().GetResult();
                        _pluginsLoaded = true;
                    }
                }
            }
        }

        public async Task<DynamicPluginInfo> GetPluginInfoForTypeAsync(DocumentProcessInfo documentProcess, Type pluginType)
        {
            await EnsurePluginsLoadedAsync();

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

                var dynamicDocumentProcessModel = await dbContext.DynamicDocumentProcessDefinitions
                    .Include(dp => dp.Plugins)
                    .ThenInclude(p => p.DynamicPlugin)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dp => dp.Id == documentProcess.Id);

                if (dynamicDocumentProcessModel == null)
                {
                    return null;
                }

                foreach (var pluginAssociation in dynamicDocumentProcessModel.Plugins)
                {
                    var plugin = pluginAssociation.DynamicPlugin;
                    if (_loadedPlugins.TryGetValue(plugin.Name, out var versionDict))
                    {
                        var versionToUse = pluginAssociation.Version ?? plugin.LatestVersion;
                        if (versionDict.TryGetValue(versionToUse, out var pluginInfo))
                        {
                            if (pluginInfo.PluginTypes.Contains(pluginType))
                            {
                                return pluginInfo;
                            }
                        }
                    }
                }

                return null;
            }
        }

        public async Task LoadDynamicPluginsAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
                var azureFileHelper = scope.ServiceProvider.GetRequiredService<AzureFileHelper>();
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
                    Console.WriteLine($"Unable to load dynamic plugins yet");
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

                        var tempDirPath = Path.Combine(Path.GetTempPath(), "greenlight-plugins", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempDirPath);

                        try
                        {
                            UnzipPlugin(pluginStream, tempDirPath);
                            var mainAssemblyPath = FindMainAssemblyPath(tempDirPath);

                            if (mainAssemblyPath != null)
                            {
                                // These assemblies will always be loaded from the default context
                                var defaultContextAssemblies = new[]
                                {
                                    "Microsoft.Extensions.Hosting",
                                    "Microsoft.Extensions.DependencyInjection",
                                    "Microsoft.Greenlight.Extensions.Plugins"
                                 };

                                var assembly = Assembly.LoadFile(mainAssemblyPath);

                                // Load all DLLs in the plugin directory to resolve dependencies
                                foreach (var dllPath in Directory.GetFiles(tempDirPath, "*.dll"))
                                {
                                    if (dllPath != mainAssemblyPath)
                                    {
                                        var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                                        // if assembly with the same name of equal or higher version is already loaded, skip loading
                                        if (AssemblyLoadContext.Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name &&
                                                                                          a.GetName().Version >= assemblyName.Version))
                                        {
                                            continue;
                                        }
                                        Assembly.LoadFile(dllPath);
                                    }
                                }

                                
                                RegisterPluginTypes(assembly);
                                var pluginInfo = CreatePluginInfo(assembly, plugin, version, tempDirPath);

                                if (!_loadedPlugins.ContainsKey(plugin.Name))
                                {
                                    _loadedPlugins[plugin.Name] = new Dictionary<DynamicPluginVersion, DynamicPluginInfo>();
                                }
                                _loadedPlugins[plugin.Name][version] = pluginInfo;
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
            }
        }

        private void RegisterPluginTypes(Assembly assembly)
        {
            const string registrationInterfaceFullName = "Microsoft.Greenlight.Extensions.Plugins.IPluginRegistration";
            var registrationTypes = GetImplementingTypes(assembly, registrationInterfaceFullName);

            foreach (var type in registrationTypes)
            {
                if (Activator.CreateInstance(type) is IPluginRegistration pluginInstance)
                {
                    pluginInstance.RegisterPlugin(_builder);
                }
            }
        }

        private DynamicPluginInfo CreatePluginInfo(Assembly assembly, DynamicPlugin plugin, DynamicPluginVersion version, string tempDirectory)
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

            return new DynamicPluginInfo
            {
                Plugin = plugin,
                Version = version,
                Assembly = assembly,
                PluginTypes = pluginTypes,
                //LoadContext = loadContext,
                TempDirectory = tempDirectory
            };
        }

        public async Task<IEnumerable<Type>> GetPluginTypesAsync(DocumentProcessInfo documentProcess)
        {
            await EnsurePluginsLoadedAsync();

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

                var dynamicDocumentProcessModel = await dbContext.DynamicDocumentProcessDefinitions
                    .Include(dp => dp.Plugins)
                    .ThenInclude(p => p.DynamicPlugin)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dp => dp.Id == documentProcess.Id);

                if (dynamicDocumentProcessModel == null)
                {
                    return Enumerable.Empty<Type>();
                }

                var result = new List<Type>();

                foreach (var pluginAssociation in dynamicDocumentProcessModel.Plugins)
                {
                    var plugin = pluginAssociation.DynamicPlugin;
                    if (_loadedPlugins.TryGetValue(plugin.Name, out var versionDict))
                    {
                        var versionToUse = pluginAssociation.Version ?? plugin.LatestVersion;
                        if (versionDict.TryGetValue(versionToUse, out var pluginInfo))
                        {
                            result.AddRange(pluginInfo.PluginTypes);
                        }
                    }
                }

                return result;
            }
        }

        private List<Type> GetImplementingTypes(Assembly assembly, string interfaceFullName)
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

        private void UnzipPlugin(Stream zipStream, string targetDirectory)
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            archive.ExtractToDirectory(targetDirectory);
        }

        private string? FindMainAssemblyPath(string directory)
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