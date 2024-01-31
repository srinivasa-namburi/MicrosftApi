// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Backend.DocumentIngestion.Shared;
using ProjectVico.Backend.DocumentIngestion.Shared.Classification;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;
using ProjectVico.Plugins.Shared.Extensions;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x => x.BuildPluginConfigurationBuilder())
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<AiOptions>().Bind(context.Configuration.GetSection("AI"));
        services.AddOptions<IngestionOptions>().Bind(context.Configuration.GetSection("Ingestion"));
        services.AddOptions<ConnectionStringOptions>().Bind(context.Configuration.GetSection("ConnectionStrings"));
        
        var aiOptions = context.Configuration.GetSection("AI").Get<AiOptions>();

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddScoped<OpenAIClient>((serviceProvider) => new OpenAIClient(
            new Uri(aiOptions.OpenAI.Endpoint),
            new AzureKeyCredential(aiOptions.OpenAI.Key)));

        services.AddScoped<DocumentAnalysisClient>((serviceProvider) => new DocumentAnalysisClient(
            new Uri(aiOptions.DocumentIntelligence.Endpoint),
            new AzureKeyCredential(aiOptions.DocumentIntelligence.Key)));

        services.AddKeyedScoped<SearchClient>("searchclient-section",
            (provider, o) => GetSearchClientWithIndex(provider, o, aiOptions.CognitiveSearch.SectionIndex));
        services.AddKeyedScoped<SearchClient>("searchclient-title",
            (provider, o) => GetSearchClientWithIndex(provider, o, aiOptions.CognitiveSearch.TitleIndex));

        services.AddScoped<IContentTreeProcessor, ContentTreeProcessor>();
        services.AddScoped<IContentTreeJsonTransformer, ContentTreeJsonTransformer>();
        services.AddScoped<IPdfPipeline, NuclearEnvironmentalReportPdfPipeline>();
        services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();

        services.AddKeyedScoped<IDocumentClassifier, NrcAdamsDocumentClassifier>("nrc-classifier");
        services.AddKeyedScoped<IDocumentClassifier, CustomDataDocumentClassifier>("customdata-classifier");
        SearchClient GetSearchClientWithIndex(IServiceProvider serviceProvider, object? key, string indexName)
        {
            var searchClient = new SearchClient(
                 new Uri(aiOptions.CognitiveSearch.Endpoint),
                         indexName,
                         new AzureKeyCredential(aiOptions.CognitiveSearch.Key));
            return searchClient;
        }
    })
    .Build();

hostBuilder.Run();
