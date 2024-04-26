using Azure.Identity;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Shared.Services.Search;
using ProjectVico.V2.Worker.DocumentIngestion.Sagas;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// This is to grant SetupManager time to perform migrations
Console.WriteLine("Waiting for SetupManager to perform migrations...");
await Task.Delay(TimeSpan.FromSeconds(15));

// Set Configuration ServiceConfigurationOptions:CognitiveServices:Endpoint to the correct value
// Read the values from the azureAiSearch Connection String
// Build an IConfigurationSection from ServiceConfigurationOptions, but set the ConnectionString to the azureAiSearch Connection String
builder.AddAzureSearchClient("aiSearch");
builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
builder.Services.AddSingleton<SearchClientFactory>();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

await builder.DelayStartup(serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices);

// Common services and dependencies
builder.AddAzureServiceBusClient("sbus");
builder.AddRabbitMQClient("rabbitmqdocgen");
builder.AddKeyedAzureOpenAIClient("openai-planner");
builder.AddAzureBlobClient("blob-docing");
builder.AddDocGenDbContext(serviceConfigurationOptions);

builder.DynamicallyRegisterPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);
builder.AddSemanticKernelServices(serviceConfigurationOptions);

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

        x.AddSagaStateMachine<KernelMemoryDocumentIngestionSaga, KernelMemoryDocumentIngestionSagaState>()
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