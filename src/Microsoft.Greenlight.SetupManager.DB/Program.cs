using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.SetupManager.DB;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<AzureCredentialHelper>();
builder.Services.AddOptions<ServiceConfigurationOptions>()
                .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
builder.Services.AddSingleton<SetupDataInitializerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SetupDataInitializerService>());
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SetupDataInitializerService.ActivitySourceName));

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName)
                                                       .Get<ServiceConfigurationOptions>()!;
// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);
var credentialHelper = new AzureCredentialHelper(builder.Configuration);
builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);


var host = builder.Build();
host.Run();