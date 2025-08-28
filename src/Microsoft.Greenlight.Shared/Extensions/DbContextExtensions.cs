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
        // Validate required configuration early to provide helpful errors instead of NullReference/Argument exceptions later
        var databaseName = serviceConfigurationOptions?.SQL?.DatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "Missing configuration: 'ServiceConfiguration:SQL:DatabaseName' must be set to the name of the connection string containing the DocGeneration database connection.");
        }

        var connectionString = builder.Configuration.GetConnectionString(databaseName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string: 'ConnectionStrings:{databaseName}' must be present and non-empty. This should point to the DocGeneration database.");
        }

        builder.Services.AddDbContext<DocGenerationDbContext>(options =>
        {
            options.UseSqlServer(
                    connectionString,
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
                connectionString,
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
            settings.ConnectionString = connectionString;
        });

        return builder;
    }

    
}
