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
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
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
        builder.Services.AddScoped<IDocumentProcessInfoService, DocumentProcessInfoService>();
        builder.Services.AddScoped<IPromptInfoService, PromptInfoService>();
        builder.Services.AddScoped<IPluginService, PluginService>();
        builder.Services.AddScoped<IDocumentLibraryInfoService, DocumentLibraryInfoService>();

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

        // Register the Kernel Memory Instance Factory for Document Libraries
        builder.Services.AddSingleton<KernelMemoryInstanceContainer>();
        builder.Services.AddScoped<IKernelMemoryInstanceFactory, KernelMemoryInstanceFactory>();

        // Register the ReviewKernelMemoryRepository
        builder.AddKernelMemoryForReviews(options);
        builder.Services.AddKeyedScoped<IKernelMemoryRepository,KernelMemoryRepository>("Reviews-IKernelMemoryRepository");
        builder.Services.AddScoped<IReviewKernelMemoryRepository, ReviewKernelMemoryRepository>();

        // Register the Additional Document Libraries Kernel Memory Repositories and services
        builder.Services.AddKeyedScoped<IKernelMemoryRepository, KernelMemoryRepository>(
            "AdditionalBase-IKernelMemoryRepository");
        builder.Services.AddScoped<IAdditionalDocumentLibraryKernelMemoryRepository, AdditionalDocumentLibraryKernelMemoryRepository>();

        // Add the Search Options Factory
        builder.Services.AddScoped<IConsolidatedSearchOptionsFactory, ConsolidatedSearchOptionsFactory>();

        // Add the Semantic Kernel Factory
        builder.Services.AddSingleton<IKernelFactory, SemanticKernelFactory>();

        // Add the Embeddings Generation Service
        builder.Services.AddScoped<IAiEmbeddingService, AiEmbeddingService>();

        // Add Content Reference Services
        builder.Services.AddContentReferenceServices();

        return builder;
    }

    /// <summary>
    /// Adds content reference services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    private static IServiceCollection AddContentReferenceServices(this IServiceCollection services)
    {
        // Register the factory
        services.AddScoped<IContentReferenceGenerationServiceFactory, ContentReferenceGenerationServiceFactory>();
            
        // Register primary content reference service
        services.AddScoped<IContentReferenceService, ContentReferenceService>();
            
        // Register content type specific generation services
        services.AddScoped<IContentReferenceGenerationService<GeneratedDocument>, GeneratedDocumentReferenceGenerationService>();

        // Add more content type specific generation services here when implemented
        // services.AddScoped<IContentReferenceGenerationService<ContentNode>, ContentNodeReferenceGenerationService>();

        // Context Builder that uses embeddings generation to build rag contexts from content references for user queries
        services.AddScoped<IRagContextBuilder, RagContextBuilder>();
            
        return services;
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
