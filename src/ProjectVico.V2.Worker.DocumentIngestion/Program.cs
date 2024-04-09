using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Worker.DocumentIngestion.AI;
using ProjectVico.V2.Worker.DocumentIngestion.Sagas;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// This is to grant SetupManager time to perform migrations
Console.WriteLine("Waiting for SetupManager to perform migrations...");
await Task.Delay(TimeSpan.FromSeconds(15));


builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

// Common services and dependencies
builder.AddAzureServiceBus("sbus");
builder.AddRabbitMQ("rabbitmqdocgen");
builder.AddKeyedAzureOpenAI("openai-planner");
builder.AddAzureBlobService("blob-docing");

builder.Services.AddKeyedScoped<SearchClient>("searchclient-section",
    (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearSectionIndex));
builder.Services.AddKeyedScoped<SearchClient>("searchclient-title",
    (provider, o) => GetSearchClientWithIndex(provider, o, serviceConfigurationOptions.CognitiveSearch.NuclearTitleIndex));

builder.DynamicallyRegisterPlugins();
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

builder.AddDocGenDbContext(serviceConfigurationOptions);

builder.AddSemanticKernelService();

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmqdocgen");

if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
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
            cfg.Host(serviceBusConnectionString, configure: config =>
            {
                config.TokenCredential = new DefaultAzureCredential();
            });
            cfg.LockDuration = TimeSpan.FromMinutes(5);
            cfg.MaxAutoRenewDuration = TimeSpan.FromMinutes(20);
            cfg.ConfigureEndpoints(context);
            cfg.ConcurrentMessageLimit = 1;
            cfg.PrefetchCount = 1;
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
