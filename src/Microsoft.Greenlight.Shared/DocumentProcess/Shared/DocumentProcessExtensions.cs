// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
// Removed legacy ReviewKernelMemoryRepository registration
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using System.Reflection;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared;

/// <summary>
/// Extension methods for registering document process related services and dynamic processes.
/// </summary>
public static class DocumentProcessExtensions
{

    /// <summary>
    /// Registers the Dynamic Document Process and its dependencies.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="serviceConfigurationOptions"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder RegisterConfiguredDocumentProcesses(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions serviceConfigurationOptions)
    {

        builder.AddCommonDocumentProcessServices(serviceConfigurationOptions);

        // Add the Dynamic Document Process
        builder.AddDocumentProcess("Dynamic", serviceConfigurationOptions);

        return builder;
    }

    private static IHostApplicationBuilder AddCommonDocumentProcessServices(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions options)
    {
        // Default Prompt Catalog Types that will resolve all prompts if they haven't been defined
        // in a DP-specific IPromptCatalogTypes implementation
        // They're also the source of new prompts in the database.
        builder.Services.AddKeyedSingleton<IPromptCatalogTypes, DefaultPromptCatalogTypes>(
            "Default-IPromptCatalogTypes");

        // Register the Generic implementation of the AiCompletionServiceParameters class
        // This holds a Db Context that's generated via the DbContext Factory.
        builder.Services.AddTransient(typeof(AiCompletionServiceParameters<>));

        // Register the Kernel Memory Instance Factory for Document Libraries
        builder.Services.AddSingleton<KernelMemoryInstanceContainer>();
        // This can be transient because it relies on the singleton KernelMemoryInstanceContainer to keep track of the instances
        builder.Services.AddTransient<IKernelMemoryInstanceFactory, KernelMemoryInstanceFactory>();

        // Register the main IKernelMemoryRepository (non-keyed) for DocumentRepositoryFactory
        builder.Services.AddTransient<IKernelMemoryRepository, KernelMemoryRepository>();
        
        // Register the Additional Document Libraries Kernel Memory Repositories and services
        builder.Services.AddKeyedTransient<IKernelMemoryRepository, KernelMemoryRepository>(
            "AdditionalBase-IKernelMemoryRepository");
        builder.Services.AddTransient<IAdditionalDocumentLibraryKernelMemoryRepository, AdditionalDocumentLibraryKernelMemoryRepository>();

        // Register the Document Repository Factory
        builder.Services.AddTransient<IDocumentRepositoryFactory, DocumentRepositoryFactory>();

        // Add the Search Options Factory
        builder.Services.AddTransient<IConsolidatedSearchOptionsFactory, ConsolidatedSearchOptionsFactory>();

        // Add the Semantic Kernel Factory
        builder.Services.AddTransient<IKernelFactory, SemanticKernelFactory>();

        // Add the Embeddings Generation & Chat Completion Service Factories
        builder.Services.AddTransient<IAiEmbeddingService, AiEmbeddingService>();
        builder.Services.AddSingleton<IChatClientFactory, CachedChatClientFactory>();

        // Add Document Generation Service
        builder.Services.AddTransient<IDocumentGenerationService, DocumentGenerationService>();

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
        try
        {

            // Try and find the assembly named "Microsoft.Greenlight.Shared" first on disk and then in loaded assemblies
            var assemblyPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "Microsoft.Greenlight.Shared.dll");

            if (!File.Exists(assemblyPath))
            {
                // If the assembly is not found on disk, try to load it from the loaded assemblies
                assemblyPath = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.FullName == "Microsoft.Greenlight.Shared")?.Location ?? string.Empty;
            }

            // If the assembly is still not found, throw an exception
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new FileNotFoundException("The assembly 'Microsoft.Greenlight.Shared' could not be found.");
            }

            var assembly = Assembly.LoadFrom(assemblyPath);

            if (assembly == null)
            {
                throw new FileNotFoundException("The assembly 'Microsoft.Greenlight.Shared' could not be loaded.");
            }

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