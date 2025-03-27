using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Core;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Shared.SagaState;
using Microsoft.Greenlight.Worker.DocumentIngestion.Sagas;

var builder = new GreenlightDynamicApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

// First add the DbContext and configuration provider
builder.AddGreenlightDbContextAndConfiguration();

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// This enables reloading:
builder.Services.Configure<ServiceConfigurationOptions>(
    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;


await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
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

    x.AddFanOutConsumersForWorkerNode();

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
            config.TokenCredential = credentialHelper.GetAzureCredential();
        });

        cfg.ConfigureEndpoints(context);
        cfg.AddFanOutSubscriptionEndpointsForWorkerNode(context);

        cfg.LockDuration = TimeSpan.FromMinutes(5);
        cfg.MaxAutoRenewDuration = TimeSpan.FromMinutes(20);

        cfg.ConcurrentMessageLimit = 1;
        cfg.PrefetchCount = 1;
        cfg.UseMessageRetry(r => r.Intervals(
        [
            // Set first retry to a random number between 3 and 9 seconds
            TimeSpan.FromSeconds(new Random().Next(3, 9)),
            // Set second retry to a random number between 10 and 30 seconds
            TimeSpan.FromSeconds(new Random().Next(10, 30)),
            // Set third and final retry to a random number between 30 and 60 seconds
            TimeSpan.FromSeconds(new Random().Next(30, 60))

        ]));
    });
});

builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

var host = builder.Build();
host.Run();
