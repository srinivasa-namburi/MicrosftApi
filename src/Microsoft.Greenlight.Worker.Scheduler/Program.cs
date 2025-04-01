using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Core;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Worker.Scheduler;
using Microsoft.Greenlight.Worker.Scheduler.Jobs;
using Quartz;

var builder = new GreenlightDynamicApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

// Add the DbContext and configuration provider
builder.AddGreenlightDbContextAndConfiguration();

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Enable reloading
builder.Services.Configure<ServiceConfigurationOptions>(
    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

var serviceConfigurationOptions = builder.Configuration
    .GetSection(ServiceConfigurationOptions.PropertyName)
    .Get<ServiceConfigurationOptions>()!;

await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.RegisterStaticPlugins(serviceConfigurationOptions);

builder.AddRepositories();
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);
builder.AddSemanticKernelServices(serviceConfigurationOptions);

// Configure Quartz
builder.Services.AddQuartz(q =>
{

    // Configure Quartz to use a thread pool with only one thread.
    q.UseDefaultThreadPool(options =>
    {
        options.MaxConcurrency = 1;
    });

    // Schedule CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob to run every hour, forever.
    q.ScheduleJob<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob>(trigger => trigger
        .WithIdentity("CreateOrUpdatePromptDefinitionsFromDefaultCatalogJobTrigger")
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInHours(1)
            .RepeatForever())
    );
    
    // Schedule the ContentReferenceIndexingJob using its configured refresh interval
    q.ScheduleJob<ContentReferenceIndexingJob>(trigger => trigger
        .WithIdentity("ContentReferenceIndexingJobTrigger")
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(serviceConfigurationOptions.GreenlightServices.ReferenceIndexing.RefreshIntervalMinutes)
            .RepeatForever())
    );

    // Schedule the ScheduledBlobAutoImportJob every x minutes (adjust interval as needed)
    q.ScheduleJob<ScheduledBlobAutoImportJob>(trigger => trigger
        .WithIdentity("ScheduledBlobAutoImportJobTrigger")
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(30)
            .RepeatForever())
    );


});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Add MassTransit configuration (if needed)
var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?
    .Replace("https://", "sb://")
    .Replace(":443/", "/");

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(typeof(Program).Assembly);

    x.AddFanOutConsumersForWorkerNode();

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(serviceBusConnectionString, configure: config =>
        {
            config.TokenCredential = credentialHelper.GetAzureCredential();
        });

        cfg.ConfigureEndpoints(context);
        cfg.AddFanOutSubscriptionEndpointsForWorkerNode(context);
        
        cfg.ConcurrentMessageLimit = 1;
        cfg.PrefetchCount = 3;
    });
});

builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

var host = builder.Build();
host.Run();
