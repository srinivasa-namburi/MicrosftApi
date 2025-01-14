using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;


namespace Microsoft.Greenlight.Shared.Extensions;


/// <summary>
/// Provides extension methods for configuring Semantic Kernel services.
/// </summary>
public static class SemanticKernelExtensions
{
    /// <summary>
    /// Adds Semantic Kernel services to the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated host application builder.</returns>
    public static IHostApplicationBuilder AddSemanticKernelServices(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {
        // UnKeyed Kernel for compatibility. Note - this loads ALL plugins, including document plugins, from all Document Processes.
        builder.Services.AddScoped<Kernel>(serviceProvider =>
        {
            KernelPluginCollection plugins = [];
            plugins.AddRegisteredPluginsToKernelPluginCollection(serviceProvider);
            var kernel = new Kernel(serviceProvider, plugins);
            return kernel;
        });

        // Get all document processes (both static and dynamic)
        var documentProcesses = GetAllDocumentProcesses(builder.Services, serviceConfigurationOptions);

        foreach (var documentProcess in documentProcesses)
        {
            builder.Services.AddKeyedScoped<Kernel>(documentProcess.ShortName + "-Kernel", (provider, o) =>
            {
                KernelPluginCollection plugins = [];

                // Add plugins from the document process
                plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(provider, documentProcess, null);

                var kernel = new Kernel(provider, plugins);
                kernel.FunctionInvocationFilters.Add(
                    provider.GetRequiredKeyedService<IFunctionInvocationFilter>("InputOutputTrackingPluginInvocationFilter"));
                return kernel;
            });
        }

        return builder;
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
                                            kmDocsPlugin.Name.Contains("native")))
        {
            kernel.Plugins.Remove(kmDocsPlugin);
        }
    }

    /// <summary>
    /// Retrieves all document processes (both static and dynamic) from the service collection and configuration options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>A list of document process information.</returns>
    private static List<DocumentProcessInfo> GetAllDocumentProcesses(IServiceCollection services, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        List<DocumentProcessInfo> documentProcesses = [];

        try
        {
            // Get dynamic document processes
            if (dbContext != null && mapper != null)
            {
                var dynamicDocumentProcesses = dbContext.DynamicDocumentProcessDefinitions
                    .Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory)
                    .Include(x => x.DocumentOutline)
                    .ToList();

                foreach (var documentProcess in dynamicDocumentProcesses)
                {
                    var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
                    documentProcesses.Add(documentProcessInfo);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Encountered error when loading Dynamic Plugins, possibly due to an in-process upgrade: {0}", e);
            throw;
        }

        // Get static document processes
        var documentProcessOptionsList = serviceConfigurationOptions.GreenlightServices.DocumentProcesses;
        foreach (var documentProcessOptions in documentProcessOptionsList)
        {
            var documentProcess = mapper!.Map<DocumentProcessInfo>(documentProcessOptions);
            documentProcesses.Add(documentProcess);
        }

        return documentProcesses;
    }
}

