using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.SetupManager;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.AddSqlServerDbContext<DocGenerationDbContext>("sqldocgen", settings =>
{
    
    settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
    settings.HealthChecks = true;
    settings.Tracing = true;
    settings.Metrics = true;
}, 
    optionsBuilder =>
{
    optionsBuilder.UseSqlServer(sqlServerBuilder =>
    {
        sqlServerBuilder.MigrationsAssembly(typeof(DocGenerationDbContext).Assembly.FullName);
    });
});


builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(DocGenDbInitializerService.ActivitySourceName));

builder.Services.AddSingleton<DocGenDbInitializerService>();

builder.Services.AddHostedService(sp=>sp.GetRequiredService<DocGenDbInitializerService>());

var host = builder.Build();
host.Run();
