using Azure.Messaging.ServiceBus.Administration;
using MassTransit;
using Microsoft.Greenlight.DocumentProcess.Shared;
using Microsoft.Greenlight.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.SetupManager.Services;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
builder.Services.AddOptions<ServiceConfigurationOptions>()
                .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

var credentialHelper = new AzureCredentialHelper(builder.Configuration);
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;
// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);
builder.AddRepositories();
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

if (!serviceConfigurationOptions.GreenlightServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.AddSemanticKernelServices(serviceConfigurationOptions);

var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmqdocgen");
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
        cfg.LockDuration = TimeSpan.FromMinutes(5);
        cfg.MaxAutoRenewDuration = TimeSpan.FromMinutes(60);
        cfg.ConfigureEndpoints(context);
        cfg.ConcurrentMessageLimit = 4;
        cfg.PrefetchCount = 3;
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

if (builder.Environment.IsDevelopment() && !serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices)
{
    await DeleteAllQueues(serviceBusConnectionString, credentialHelper);
}

builder.Services.AddSingleton<SetupServicesInitializerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SetupServicesInitializerService>());

var host = builder.Build();
host.Run();

static async Task DeleteAllQueues(string? connectionString, AzureCredentialHelper credentialHelper)
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