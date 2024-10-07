using System.Reflection;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.DocumentProcess.Shared.Prompts;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using TableHelper = Microsoft.Greenlight.Shared.Helpers.TableHelper;

namespace Microsoft.Greenlight.DocumentProcess.Shared;

public static class DocumentProcessExtensions
{

    /// <summary>
    /// Registers all static Document Processes defined in the ServiceConfigurationOptions.DocumentProcesses property ("US.NuclearLicensing" only by default)
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="serviceConfigurationOptions"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder RegisterConfiguredDocumentProcesses(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {

        builder.AddCommonDocumentProcessServices(serviceConfigurationOptions);

        var documentProcesses = serviceConfigurationOptions.GreenlightServices.DocumentProcesses;

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
        // Document Info Service and associated mappings
        builder.Services.AddScoped<IDocumentProcessInfoService, DocumentProcessInfoService>();
        builder.Services.AddScoped<IPromptInfoService, PromptInfoService>();
        builder.Services.AddScoped<DocumentAnalysisClient>((serviceProvider) => new DocumentAnalysisClient(
            new Uri(options.DocumentIntelligence.Endpoint),
            new AzureKeyCredential(options.DocumentIntelligence.Key)));

        // Ingestion specific custom dependencies
        builder.Services.AddScoped<TableHelper>();
        builder.Services.AddScoped<AzureFileHelper>();

        // Services included for backwards compatibility with older Document Processes ("Classic" Document Processes)
        builder.Services.AddSingleton<IContentTreeProcessor, ContentTreeProcessor>();
        builder.Services.AddSingleton<IIndexingProcessor, SearchIndexingProcessor>();

        // Default Prompt Catalog Types that will resolve all prompts if they haven't been defined
        // in a DP-specific IPromptCatalogTypes implementation
        // They're also the source of new prompts in the database.
        builder.Services.AddKeyedSingleton<IPromptCatalogTypes, DefaultPromptCatalogTypes>(
            "Default-IPromptCatalogTypes");

        // Register the Generic implementation of the AiCompletionServiceParameters class
        builder.Services.AddSingleton(typeof(AiCompletionServiceParameters<>));

        // Register the ReviewKernelMemoryRepository
        builder.AddKernelMemoryForReviews(options);
        builder.Services.AddKeyedScoped<IKernelMemoryRepository,KernelMemoryRepository>("Reviews-IKernelMemoryRepository");
        builder.Services.AddScoped<IReviewKernelMemoryRepository, ReviewKernelMemoryRepository>();
        
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

        // Filter for assemblies starting with 'Microsoft.Greenlight.DocumentProcess.' and ending with documentProcessName
        // We only want to return a single assembly, or throw an exception if there are multiple
        if (!Directory.Exists(baseDirectory))
        {
            throw new DirectoryNotFoundException(baseDirectory);
        }

        var assemblyPath = Directory
            .GetFiles(baseDirectory, "Microsoft.Greenlight.DocumentProcess.*.dll")
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

   
}
