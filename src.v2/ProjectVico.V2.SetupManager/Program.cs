using ProjectVico.V2.SetupManager;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.AddDocGenDbContext(serviceConfigurationOptions);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(DocGenDbInitializerService.ActivitySourceName));

builder.Services.AddSingleton<DocGenDbInitializerService>();

builder.Services.AddHostedService(sp=>sp.GetRequiredService<DocGenDbInitializerService>());

var host = builder.Build();
host.Run();
