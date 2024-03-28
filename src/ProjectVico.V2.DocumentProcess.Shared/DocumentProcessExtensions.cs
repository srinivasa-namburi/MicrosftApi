using System.Reflection;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Mapping;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Services.Search;

namespace ProjectVico.V2.DocumentProcess.Shared;

public static class DocumentProcessExtensions
{

    /// <summary>
    /// Registers all Document Processes defined in the ServiceConfigurationOptions.DocumentProcesses property ("US.NuclearLicensing" only by default)
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder RegisterConfiguredDocumentProcesses(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions options)
    {

        builder.AddCommonDocumentProcessServices(options);

        var documentProcesses = options.ProjectVicoServices.DocumentProcesses;
        foreach (var documentProcess in documentProcesses)
        {
            builder.AddDocumentProcess(documentProcess.Name, options);
        }

        return builder;
    }

    public static IHostApplicationBuilder AddCommonDocumentProcessServices(this IHostApplicationBuilder builder,
        ServiceConfigurationOptions options)
    {
        // DocumentAnalysisClient, TableProfile, AzureFileHelper and IContentTreeProcessor
        builder.Services.AddScoped<DocumentAnalysisClient>((serviceProvider) => new DocumentAnalysisClient(
            new Uri(options.DocumentIntelligence.Endpoint),
            new AzureKeyCredential(options.DocumentIntelligence.Key)));
        
        // Object Mapping with AutoMapper
        builder.Services.AddAutoMapper(typeof(TableProfile), typeof(MetadataProfile));

        // Ingestion specific custom dependencies
        builder.Services.AddScoped<TableHelper>();
        builder.Services.AddScoped<AzureFileHelper>();

        builder.Services.AddScoped<IContentTreeProcessor, ContentTreeProcessor>();
        // TODO: This needs to be broken apart into several services, with actual indexing being
        // part of each specific document process and the common functionality being in a separate service
        builder.Services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();

        //Basic metadata service
        builder.Services
            .AddScoped<IMetadataService, MetadataService>();

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
    public static IHostApplicationBuilder AddDocumentProcess(
        this IHostApplicationBuilder builder,
        string documentProcessName,
        ServiceConfigurationOptions options)
    {
        // Define the base directory - assuming it's the current directory for simplicity
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Filter for assemblies starting with 'ProjectVico.V2.DocumentProcess.' and ending with documentProcessName
        // We only want to return a single assembly, or throw an exception if there are multiple
        if (!Directory.Exists(baseDirectory))
        {
            throw new DirectoryNotFoundException(baseDirectory);

        }

        var assemblyPath = Directory.GetFiles(baseDirectory, "ProjectVico.V2.DocumentProcess.*.dll")
            .Where(path => path.Contains(documentProcessName))
            .FirstOrDefault();

        if (assemblyPath == null)
        {
            throw new FileNotFoundException($"Could not find an assembly for the document process {documentProcessName}");
        }

        try
        {
            // Load the assembly
            var assembly = Assembly.LoadFrom(assemblyPath);

            // Check and register plugins from the assembly
            var documentProcessRegistrationTypes = assembly.GetTypes()
                .Where(t => typeof(IDocumentProcessRegistration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            foreach (var type in documentProcessRegistrationTypes)
            {
                if (Activator.CreateInstance(type) is IDocumentProcessRegistration pluginInstance)
                {
                    builder = pluginInstance.RegisterDocumentProcess(builder, options);
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