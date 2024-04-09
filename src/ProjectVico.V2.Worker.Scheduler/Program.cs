using Azure.Identity;
using MassTransit;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Worker.Scheduler;

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
builder.AddAzureBlobService("blob-docing");

// Ingestion specific custom dependencies
builder.Services.AddScoped<AzureFileHelper>();

builder.AddDocGenDbContext(serviceConfigurationOptions);

// Add Service Bus Connection string. Replace https:// with sb:// and replace :443/ with / at the end of the connection string
var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmqdocgen");

if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);
        
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(serviceBusConnectionString, configure: config =>
            {
                config.TokenCredential = new DefaultAzureCredential();
            });
            cfg.ConfigureEndpoints(context);
            cfg.ConcurrentMessageLimit = 1;
            cfg.PrefetchCount = 3;
        });
    });
}
else
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.PrefetchCount = 1;
            cfg.ConcurrentMessageLimit = 1;
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

builder.Services.AddHostedService<ScheduledBlobAutoImportWorker>();
builder.Services.AddHostedService<ScheduledSignalRKeepAliveWorker>();

var host = builder.Build();
host.Run();
