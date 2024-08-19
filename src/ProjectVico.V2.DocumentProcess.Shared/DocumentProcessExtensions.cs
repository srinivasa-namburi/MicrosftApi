using System.Reflection;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Markdig.Extensions.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Prompts;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Services;
using ProjectVico.V2.Shared.Services.Search;
using TableHelper = ProjectVico.V2.Shared.Helpers.TableHelper;

namespace ProjectVico.V2.DocumentProcess.Shared;

public static class DocumentProcessExtensions
{

    /// <summary>
    /// Registers all Document Processes defined in the ServiceConfigurationOptions.DocumentProcesses property ("US.NuclearLicensing" only by default)
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="serviceConfigurationOptions"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder RegisterConfiguredDocumentProcesses(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {

        builder.AddCommonDocumentProcessServices(serviceConfigurationOptions);

        var documentProcesses = serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses;

        if (documentProcesses.Count > 0)
        {
            foreach (var documentProcess in documentProcesses)
            {
                if (documentProcess?.Name != null)
                    builder.AddDocumentProcess(documentProcess?.Name!, serviceConfigurationOptions);
            }
        }

        // Add the Dynamic Document Process
        builder.AddDocumentProcess("Dynamic", serviceConfigurationOptions);

        return builder;
    }

    private static IHostApplicationBuilder AddCommonDocumentProcessServices(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions options)
    {
        // DocumentAnalysisClient, TableProfile, AzureFileHelper and IContentTreeProcessor
        builder.Services.AddScoped<DocumentAnalysisClient>((serviceProvider) => new DocumentAnalysisClient(
            new Uri(options.DocumentIntelligence.Endpoint),
            new AzureKeyCredential(options.DocumentIntelligence.Key)));

        // Table mapping to and from Document Intelligence
        builder.Services.AddAutoMapper(typeof(TableProfile));

        // Ingestion specific custom dependencies
        builder.Services.AddScoped<TableHelper>();
        builder.Services.AddScoped<AzureFileHelper>();

        builder.Services.AddSingleton<IContentTreeProcessor, ContentTreeProcessor>();
        builder.Services.AddSingleton<IIndexingProcessor, SearchIndexingProcessor>();

        // Default Prompt Catalog Types that will resolve all prompts if they haven't been defined
        // in a DP-specific IPromptCatalogTypes implementation
        builder.Services.AddKeyedSingleton<IPromptCatalogTypes, DefaultPromptCatalogTypes>(
            "Default-IPromptCatalogTypes");

        return builder;
    }

    /// <summary>
    /// Register a Document Process for use in the Document Processing Pipeline
    /// Which Document Process to use is determined by the DocumentProcessName property on the initial message in most cases
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="documentProcessName"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    private static IHostApplicationBuilder AddDocumentProcess(
        this IHostApplicationBuilder builder,
        string documentProcessName,
        ServiceConfigurationOptions options)
    {
        // Define the base directory - assuming it's the current directory for simplicity
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Filter for assemblies starting with 'ProjectVico.V2.DocumentProcess.' and ending with documentProcessName
        // We only want to return a single assembly, or throw an exception if there are multiple
        if (!Directory.Exists(baseDirectory))
        {
            throw new DirectoryNotFoundException(baseDirectory);
        }

        var assemblyPath = Directory
            .GetFiles(baseDirectory, "ProjectVico.V2.DocumentProcess.*.dll")
            .FirstOrDefault(path => path.Contains(documentProcessName));

        if (assemblyPath == null)
        {
            throw new FileNotFoundException($"Could not find an assembly for the document process {documentProcessName}");
        }

        try
        {
            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Load DocumentProcess Registrations from the assembly
            var documentProcessRegistrationTypes = assembly.GetTypes()
                .Where(t => typeof(IDocumentProcessRegistration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            // Register each Document Process found in the assembly through its IDocumentProcessRegistration implementation
            foreach (var type in documentProcessRegistrationTypes)
            {
                if (Activator.CreateInstance(type) is IDocumentProcessRegistration documentProcessRegistrationInstance)
                {
                    builder = documentProcessRegistrationInstance.RegisterDocumentProcess(builder, options);
                }
            }
        }
        catch (Exception ex)
        {
            // Handle or log exceptions as appropriate
            Console.WriteLine($"Error loading assembly or registering plugins: {ex.Message}");
        }


        return builder;
    }

    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
    }

    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, DocumentProcessInfo documentProcessInfo)
    {
        var service = sp.GetRequiredServiceForDocumentProcess<T>(documentProcessInfo.ShortName);
        return service;
    }

    public static T GetRequiredServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        var service = sp.GetServiceForDocumentProcess<T>(documentProcessName);
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found for document process {documentProcessName}");
        }
        return service;
    }
    public static T? GetServiceForDocumentProcess<T>(this IServiceProvider sp, string documentProcessName)
    {
        T? service = default;

        var scope = sp.CreateScope();

        // Try to get a scoped service for the specific document process, then the default service, then finally a service with no key.
        // This allows for a service to be registered for a specific document process, or a default service to be registered for all document processes,
        // or a service to be registered with no key for use outside of the document process context.
        service = scope.ServiceProvider.GetKeyedService<T>($"{documentProcessName}-{typeof(T).Name}") ??
                  scope.ServiceProvider.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                  scope.ServiceProvider.GetService<T>();

        // If the service is still null - it may not be scoped but exists as a singleton. Try to get the singleton service.
        if (service == null)
        {
            service = sp.GetKeyedService<T>($"{documentProcessName}-{typeof(T).Name}") ??
                      sp.GetKeyedService<T>($"Default-{typeof(T).Name}") ??
                      sp.GetService<T>();
        }

        return service;
    }
}