using Azure.Messaging.ServiceBus.Administration;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.Plugins.Shared;
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

builder.AddProjectVicoServices(credentialHelper, serviceConfigurationOptions);

if (!serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.DynamicallyRegisterPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);
builder.AddSemanticKernelServices(serviceConfigurationOptions);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SetupDataInitializerService.ActivitySourceName));

builder.Services.AddSingleton<SetupDataInitializerService>();

if (builder.Environment.IsDevelopment() && !serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices)
{
    var sbusConnectionString = builder.Configuration.GetConnectionString("sbus");
    await DeleteAllQueues(sbusConnectionString, credentialHelper);
}

builder.Services.AddHostedService(sp => sp.GetRequiredService<SetupDataInitializerService>());


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