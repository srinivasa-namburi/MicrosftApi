using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.SetupManager.DB;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

builder.Services.AddSingleton<AzureCredentialHelper>();
builder.Services.AddOptions<ServiceConfigurationOptions>()
                .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

builder.Services.AddSingleton<SetupDataInitializerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SetupDataInitializerService>());
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SetupDataInitializerService.ActivitySourceName));

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName)
                                                       .Get<ServiceConfigurationOptions>()!;
builder.AddDocGenDbContext(serviceConfigurationOptions);

// Register only the minimal services needed for setup/seeding to avoid pulling in
// Orleans, AI, and other heavy dependencies during migrations.
builder.Services.AddAutoMapper(typeof(Microsoft.Greenlight.Shared.Mappings.FlowTaskMappingProfile));
builder.Services.AddTransient<IPromptDefinitionService, PromptDefinitionService>();
builder.Services.AddTransient<IFlowTaskTemplateService, FlowTaskTemplateService>();


var host = builder.Build();
host.Run();