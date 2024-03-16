using Azure.Identity;
using Azure.Messaging.ServiceBus.Administration;
using ProjectVico.V2.SetupManager;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureServiceBus("sbus");

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.AddDocGenDbContext(serviceConfigurationOptions);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(DocGenDbInitializerService.ActivitySourceName));

builder.Services.AddSingleton<DocGenDbInitializerService>();

if (builder.Environment.IsDevelopment() && !serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices)
{
    var sbusConnectionString = builder.Configuration.GetConnectionString("sbus");
    await DeleteAllQueues(sbusConnectionString);
}

builder.Services.AddHostedService(sp => sp.GetRequiredService<DocGenDbInitializerService>());

var host = builder.Build();
host.Run();

async Task DeleteAllQueues(string? connectionString)
{
    var adminClient = new ServiceBusAdministrationClient(connectionString, new DefaultAzureCredential());

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