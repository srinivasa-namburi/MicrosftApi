using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectVico.V2.Shared.Data;

public static class DbContextExtensions
{
    public static IHostApplicationBuilder AddDocGenDbContext(this IHostApplicationBuilder builder, ServiceConfigurationOptions serviceConfigurationOptions)
    {

        builder.Services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();

        builder.AddSqlServerDbContext<DocGenerationDbContext>("sqldocgen", settings =>
            {
                settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
                settings.HealthChecks = true;
                settings.Tracing = true;
                settings.Metrics = true;
            }, 
            optionsBuilder =>
            {
                //optionsBuilder.AddInterceptors(new SoftDeleteInterceptor());
                optionsBuilder.UseSqlServer(sqlServerBuilder =>
                {
                    sqlServerBuilder.MigrationsAssembly(typeof(DocGenerationDbContext).Assembly.FullName);
                });
            });

        return builder;
    }
}