using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.SetupManager;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Helpers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.Services.AddSingleton<SetupDataInitializerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SetupDataInitializerService>());

builder.AddProjectVicoServices(credentialHelper, serviceConfigurationOptions);
builder.DynamicallyRegisterPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

if (!serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.AddSemanticKernelServicesForStaticDocumentProcesses(serviceConfigurationOptions);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SetupDataInitializerService.ActivitySourceName));


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
                config.TokenCredential = credentialHelper.GetAzureCredential();
            });
            cfg.LockDuration = TimeSpan.FromMinutes(5);
            cfg.MaxAutoRenewDuration = TimeSpan.FromMinutes(60);
            cfg.ConfigureEndpoints(context);
            cfg.ConcurrentMessageLimit = 4;
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
        
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.PrefetchCount = 3;
            cfg.ConcurrentMessageLimit = 4;
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}


if (builder.Environment.IsDevelopment() && !serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices)
{
   await DeleteAllQueues(serviceBusConnectionString, credentialHelper);
}



var host = builder.Build();
host.Run();

async Task DeleteAllQueues(string? connectionString, AzureCredentialHelper credentialHelper)
{
    var adminClient = new ServiceBusAdministrationClient(
        connectionString, 
        credentialHelper.GetAzureCredential());

    // Delete all queues
    await foreach (var queue in adminClient.GetQueuesAsync())
    {
        try
        {
            Console.WriteLine($"Deleting queue: {queue.Name}");
            await adminClient.DeleteQueueAsync(queue.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete queue {queue.Name}: {ex.Message}");
        }
    }

    // Delete all topics
    await foreach (var topic in adminClient.GetTopicsAsync())
    {
        try
        {
            Console.WriteLine($"Deleting topic: {topic.Name}");
            await adminClient.DeleteTopicAsync(topic.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete topic {topic.Name}: {ex.Message}");
        }
    }

    Console.WriteLine("All queues and topics have been deleted.");
}