using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Helpers;

namespace Microsoft.Greenlight.Shared.Extensions;

/// <summary>
/// Provides extension methods for configuring DbContext in an IHostApplicationBuilder.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Adds the DocGenerationDbContext to the IHostApplicationBuilder.
    /// </summary>
    /// <param name="builder">The IHostApplicationBuilder to add the DbContext to.</param>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <returns>The updated IHostApplicationBuilder.</returns>
    public static IHostApplicationBuilder AddDocGenDbContext(this IHostApplicationBuilder builder, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        builder.Services.AddDbContext<DocGenerationDbContext>(options =>
        {
            options.UseSqlServer(
                    builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName),
                    sqlServerBuilder =>
                    {
                        sqlServerBuilder.MigrationsAssembly(typeof(DocGenerationDbContext).Assembly.FullName);
                    });

            if (!AdminHelper.IsRunningInProduction())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Add a factory for creating DbContext instances on demand
        // This is important to maintain scope in Orleans Grains calls
        builder.Services.AddSingleton<IDbContextFactory<DocGenerationDbContext>>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<DocGenerationDbContext>();
            optionsBuilder.UseSqlServer(
                builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName),
                sqlServerBuilder =>
                {
                    sqlServerBuilder.MigrationsAssembly(typeof(DocGenerationDbContext).Assembly.FullName);
                });

            if (!AdminHelper.IsRunningInProduction())
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
            }

            return new PooledDbContextFactory<DocGenerationDbContext>(optionsBuilder.Options);
        });

        builder.EnrichSqlServerDbContext<DocGenerationDbContext>(settings =>
        {
            settings.DisableRetry = true;
            settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
        });

        return builder;
    }

    
}
