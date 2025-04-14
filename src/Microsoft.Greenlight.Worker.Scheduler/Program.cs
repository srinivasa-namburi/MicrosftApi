using MassTransit;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Core;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Worker.Scheduler.Jobs;
using Quartz;

#pragma warning disable CS0618

var builder = new GreenlightDynamicApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

// Add the DbContext and configuration provider
builder.AddGreenlightDbContextAndConfiguration();

var serviceConfigurationOptions = builder.Configuration
    .GetSection(ServiceConfigurationOptions.PropertyName)
    .Get<ServiceConfigurationOptions>()!;

await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.RegisterStaticPlugins(serviceConfigurationOptions);

builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

builder.AddGreenlightOrleansClient(credentialHelper);

// Configure Quartz
builder.Services.AddQuartz(q =>
{
    // Configure DI for Quartz jobs - this creates a new scope for each job execution
    q.UseMicrosoftDependencyInjectionJobFactory(options =>
    {
        // Create a new scope for each job
        options.CreateScope = true;
    });
    
    // Register job classes
    q.AddJob<RepositoryIndexMaintenanceJob>(opts => opts
        .WithIdentity(nameof(RepositoryIndexMaintenanceJob))
        .StoreDurably());
        
    q.AddJob<CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob>(opts => opts
        .WithIdentity(nameof(CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob))
        .StoreDurably());
        
    q.AddJob<ContentReferenceIndexingJob>(opts => opts
        .WithIdentity(nameof(ContentReferenceIndexingJob))
        .StoreDurably());
        
    q.AddJob<ScheduledBlobAutoImportJob>(opts => opts
        .WithIdentity(nameof(ScheduledBlobAutoImportJob))
        .StoreDurably());

    // Schedule triggers for each job
    q.AddTrigger(trigger => trigger
        .WithIdentity(nameof(RepositoryIndexMaintenanceJob) + "Trigger")
        .ForJob(nameof(RepositoryIndexMaintenanceJob))
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(1)
            .RepeatForever())
    );

    q.AddTrigger(trigger => trigger
        .WithIdentity(nameof(CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob) + "Trigger")
        .ForJob(nameof(CreateOrUpdatePromptDefinitionsFromDefaultCatalogJob))
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInHours(1)
            .RepeatForever())
    );
    
    q.AddTrigger(trigger => trigger
        .WithIdentity(nameof(ContentReferenceIndexingJob) + "Trigger")
        .ForJob(nameof(ContentReferenceIndexingJob))
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(serviceConfigurationOptions.GreenlightServices.ReferenceIndexing.RefreshIntervalMinutes)
            .RepeatForever())
    );

    q.AddTrigger(trigger => trigger
        .WithIdentity(nameof(ScheduledBlobAutoImportJob) + "Trigger")
        .ForJob(nameof(ScheduledBlobAutoImportJob))
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(30)
            .RepeatForever())
    );
});

Console.WriteLine("Delaying 5 seconds for configuration to load fully");
await Task.Delay(TimeSpan.FromSeconds(5));

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

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// This enables reloading:
builder.Services.Configure<ServiceConfigurationOptions>(
    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

builder.Services.AddGreenlightHostedServices();

var host = builder.Build();
host.Run();
