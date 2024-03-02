using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.SetupManager;
using ProjectVico.V2.Shared.Data.Sql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<DocGenerationDbContext>("sql-docgen", settings =>
{
    settings.ConnectionString = builder.Configuration.GetConnectionString("ProjectVICOdb");
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
