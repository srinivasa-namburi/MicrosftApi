using MassTransit;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;

//var builder = new GreenlightDynamicApplicationBuilder(args);

var builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

// First add the DbContext and configuration provider
builder.AddGreenlightDbContextAndConfiguration();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);

if (!serviceConfigurationOptions.GreenlightServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddScoped<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.RegisterStaticPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

/* Keep Masstransit Present because some common services depend on being able to send still */
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(typeof(Program).Assembly);
    x.AddFanOutConsumersForNonWorkerNode();
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(serviceBusConnectionString, configure: config =>
        {
            config.TokenCredential = credentialHelper.GetAzureCredential();
        });
        cfg.ConfigureEndpoints(context);
        cfg.AddFanOutSubscriptionEndpointsForNonWorkerNode(context);

    });
});

builder.AddGreenLightOrleansSilo(credentialHelper);

Console.WriteLine("Delaying 5 seconds for configuration to load fully");
await Task.Delay(TimeSpan.FromSeconds(5));

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