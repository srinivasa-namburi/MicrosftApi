using MassTransit;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Worker.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.RegisterStaticPlugins(serviceConfigurationOptions);

builder.AddRepositories();
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);
builder.AddSemanticKernelServices(serviceConfigurationOptions);

builder.Services.AddHostedService<ScheduledBlobAutoImportWorker>();
builder.Services.AddHostedService<DynamicDocumentProcessMaintenanceWorker>();
builder.Services.AddHostedService<ScheduledExportedDocumentCleanupWorker>();

// Add Service Bus Connection string. Replace https:// with sb:// and replace :443/ with / at the end of the connection string
var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(typeof(Program).Assembly);

    x.AddConsumer<RestartWorkerConsumer>();

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
        cfg.ConfigureEndpoints(context);
        cfg.ConcurrentMessageLimit = 1;
        cfg.PrefetchCount = 3;
    });
});

builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

var host = builder.Build();
host.Run();
