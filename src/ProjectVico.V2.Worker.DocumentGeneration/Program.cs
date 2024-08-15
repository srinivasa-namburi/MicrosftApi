using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Shared.Services.Search;
using ProjectVico.V2.Worker.DocumentGeneration.Sagas;


var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.AddAzureSearchClient("aiSearch");
builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
builder.Services.AddSingleton<SearchClientFactory>();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

await builder.DelayStartup(serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddAzureServiceBusClient("sbus");
builder.AddRabbitMQClient("rabbitmqdocgen");
builder.AddKeyedAzureOpenAIClient("openai-planner");
builder.AddAzureBlobClient("blob-docing");
builder.AddRedisClient("redis");

builder.AddDocGenDbContext(serviceConfigurationOptions);

if (!serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

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

         x.AddSagaStateMachine<DocumentGenerationSaga, DocumentGenerationSagaState>()
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
             cfg.MaxAutoRenewDuration = TimeSpan.FromMinutes(60);
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

        x.AddSagaStateMachine<DocumentGenerationSaga, DocumentGenerationSagaState>()
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
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

var host = builder.Build();
host.Run();