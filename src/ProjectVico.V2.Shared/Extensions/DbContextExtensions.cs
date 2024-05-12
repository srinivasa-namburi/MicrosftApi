using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using ProjectVico.V2.Shared.Data;

namespace ProjectVico.V2.Shared.Extensions;

public static class DbContextExtensions
{
    public static IHostApplicationBuilder AddDocGenDbContext(this IHostApplicationBuilder builder, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.Services.AddSingleton<SoftDeleteInterceptor>();

        var sp = builder.Services.BuildServiceProvider();

        builder.Services.AddDbContext<DocGenerationDbContext>(options =>
        {
            options.UseSqlServer(
                builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName),
                sqlServerBuilder =>
                {
                    sqlServerBuilder.MigrationsAssembly(typeof(DocGenerationDbContext).Assembly.FullName);
                });

            options.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        builder.EnrichSqlServerDbContext<DocGenerationDbContext>(settings =>
        {
            settings.DisableRetry = false;
            settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
                        
        });

        return builder;
    }
}