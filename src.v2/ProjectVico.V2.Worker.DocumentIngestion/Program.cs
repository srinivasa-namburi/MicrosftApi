using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Search.Documents;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.Shared.Classification.Classifiers;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Pipelines;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Shared.Search;
using ProjectVico.V2.Worker.DocumentIngestion.AI;
using ProjectVico.V2.Worker.DocumentIngestion.Sagas;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection("ServiceConfiguration"));
var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;

// Common services and dependencies
builder.AddAzureServiceBus("sbus");
builder.AddRabbitMQ("rabbitmq-docgen");
builder.AddKeyedAzureOpenAI("openai-planner");
builder.AddAzureBlobService("blob-docing");
builder.Services.AddScoped<DocumentAnalysisClient>((serviceProvider) => new DocumentAnalysisClient(
    new Uri(serviceConfigurationOptions.DocumentIntelligence.Endpoint),
    new AzureKeyCredential(serviceConfigurationOptions.DocumentIntelligence.Key)));

// Object Mapping with AutoMapper
builder.Services.AddAutoMapper(typeof(TableProfile));

// Ingestion specific custom dependencies
builder.Services.AddScoped<AzureFileHelper>();
builder.Services.AddScoped<TableHelper>();

builder.Services.AddKeyedScoped<SearchClient>("searchclient-section",
    (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearSectionIndex));
builder.Services.AddKeyedScoped<SearchClient>("searchclient-title",
    (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearTitleIndex));
builder.Services.AddKeyedScoped<SearchClient>("searchclient-customdata",
    (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.CustomIndex));

builder.Services.AddKeyedScoped<IDocumentClassifier, NrcAdamsDocumentClassifier>("nrc-classifier");
builder.Services.AddKeyedScoped<IDocumentClassifier, CustomDataDocumentClassifier>("customdata-classifier");

builder.Services.AddScoped<IContentTreeProcessor, ContentTreeProcessor>();
builder.Services.AddScoped<IIndexingProcessor, SearchIndexingProcessor>();

builder.Services.AddKeyedScoped<IPdfPipeline, BaselinePipeline>("baseline-pipeline");
builder.Services.AddKeyedScoped<IPdfPipeline, NuclearEnvironmentalReportPdfPipeline>("nuclear-er-pipeline");


builder.AddSqlServerDbContext<DocGenerationDbContext>("sql-docgen", settings =>
{
    settings.ConnectionString = builder.Configuration.GetConnectionString("ProjectVICOdb");
    settings.DbContextPooling = true;
    settings.HealthChecks = true;
    settings.Tracing = true;
    settings.Metrics = true;
});

builder.AddSemanticKernelService();

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq-docgen");

if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(serviceBusConnectionString))
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);

        x.AddSagaStateMachine<DocumentIngestionSaga, DocumentIngestionSagaState>()
            .EntityFrameworkRepository(cfg =>
            {
                cfg.ExistingDbContext<DocGenerationDbContext>();
                cfg.LockStatementProvider =
                    new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
            });

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(serviceBusConnectionString);
            cfg.ConfigureEndpoints(context);
            cfg.ConcurrentMessageLimit = 1;
            cfg.PrefetchCount = 3;
            cfg.UseMessageRetry(r => r.Intervals(new TimeSpan[]
            {
                // Set first retry to a random number between 3 and 9 seconds
                TimeSpan.FromSeconds(new Random().Next(3, 9)),
                // Set second retry to a random number between 10 and 30 seconds
                TimeSpan.FromSeconds(new Random().Next(10, 30)),
                // Set third and final retry to a random number between 30 and 60 seconds
                TimeSpan.FromSeconds(new Random().Next(30, 60))
                
            }));
        });
    });
}
else
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);

        x.AddSagaStateMachine<DocumentIngestionSaga, DocumentIngestionSagaState>()
            .EntityFrameworkRepository(cfg =>
            {
                cfg.ExistingDbContext<DocGenerationDbContext>();
                cfg.LockStatementProvider =
                    new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
            });

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.PrefetchCount = 1;
            cfg.ConcurrentMessageLimit = 1;
            cfg.UseMessageRetry(r => r.Intervals(new TimeSpan[]
            {
                // Set first retry to a random number between 3 and 9 seconds
                TimeSpan.FromSeconds(new Random().Next(3, 9)),
                // Set second retry to a random number between 10 and 30 seconds
                TimeSpan.FromSeconds(new Random().Next(10, 30)),
                // Set third and final retry to a random number between 45 and 120 seconds
                TimeSpan.FromSeconds(new Random().Next(45, 120))
                
            }));
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}



var host = builder.Build();
host.Run();

SearchClient GetSearchClientWithIndex(IServiceProvider serviceProvider, object? key, string indexName)
{
    var searchClient = new SearchClient(
        new Uri(serviceConfigurationOptions.CognitiveSearch.Endpoint),
        indexName,
        new AzureKeyCredential(serviceConfigurationOptions.CognitiveSearch.Key));
    return searchClient;
}
