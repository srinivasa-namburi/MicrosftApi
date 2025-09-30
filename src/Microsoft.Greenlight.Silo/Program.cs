using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Grains.Shared.Scheduling;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;

// Use the standard HostApplicationBuilder instead of the custom class
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration and validate developer setup
AdminHelper.Initialize(builder.Configuration);
AdminHelper.ValidateDeveloperSetup("Silo");

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

builder.AddGreenLightOrleansSilo(credentialHelper);

Console.WriteLine("Delaying 5 seconds for configuration to load fully");
await Task.Delay(TimeSpan.FromSeconds(5));

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .PostConfigure(o =>
    {
        o.GreenlightServices.VectorStore.StoreType = o.GreenlightServices.Global.UsePostgresMemory
            ? VectorStoreType.PostgreSQL
            : VectorStoreType.AzureAISearch;
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

//// This enables reloading:
//builder.Services.Configure<ServiceConfigurationOptions>(
//    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
// Add this to provide a singleton instance directly:
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<ServiceConfigurationOptions>>().Value);

builder.Services.AddGreenlightHostedServices();

// Run the scheduler initialization service
builder.Services.AddHostedService<SchedulerStartupService>();

var host = builder.Build();
host.Run();