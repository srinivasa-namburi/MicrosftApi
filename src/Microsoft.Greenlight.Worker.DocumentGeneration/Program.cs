using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.SagaState;
using Microsoft.Greenlight.Worker.DocumentGeneration.Sagas;
using Microsoft.Greenlight.Shared.Core;


var builder = new GreenlightDynamicApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);

if (!serviceConfigurationOptions.GreenlightServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.RegisterStaticPlugins(serviceConfigurationOptions);
builder.AddRepositories();
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

builder.AddSemanticKernelServices(serviceConfigurationOptions);

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

builder.Services.AddMassTransit(x =>
 {
     x.SetKebabCaseEndpointNameFormatter();
     x.AddConsumers(typeof(Program).Assembly);

     x.AddConsumer<RestartWorkerConsumer>();

     x.AddSagaStateMachine<DocumentGenerationSaga, DocumentGenerationSagaState>()
         .EntityFrameworkRepository(cfg =>
         {
             cfg.ExistingDbContext<DocGenerationDbContext>();
             cfg.LockStatementProvider =
                 new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
         });

     x.AddSagaStateMachine<ReviewExecutionSaga, ReviewExecutionSagaState>()
         .EntityFrameworkRepository(cfg =>
         {
             cfg.ExistingDbContext<DocGenerationDbContext>();
             cfg.LockStatementProvider =
                 new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
         });

     x.UsingAzureServiceBus((context, cfg) =>
     {
         // Register the restart worker subscription for this node
         var subscriptionName = RestartWorkerConsumer.GetRestartWorkerEndpointName();

         cfg.SubscriptionEndpoint<RestartWorker>(subscriptionName, e =>
         {
             e.ConfigureConsumer<RestartWorkerConsumer>(context);
         });

         cfg.Host(serviceBusConnectionString, configure: config =>
         {
             config.TokenCredential = credentialHelper.GetAzureCredential();
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


builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

var host = builder.Build();
host.Run();