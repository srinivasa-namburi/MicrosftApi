using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Repositories;

namespace Microsoft.Greenlight.Shared.Extensions;

public static class DbContextExtensions
{
    public static IHostApplicationBuilder AddDocGenDbContext(this IHostApplicationBuilder builder, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        var sp = builder.Services.BuildServiceProvider();

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

        builder.EnrichSqlServerDbContext<DocGenerationDbContext>(settings =>
        {
            settings.DisableRetry = true;
            settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);

        });

        builder.AddRepositories();

        if (!AdminHelper.IsRunningInProduction())
        {
            builder.Logging.AddConsole()
                .AddFilter((category, level) =>
                    category == DbLoggerCategory.Database.Command.Name
                    && level == LogLevel.Information);

        }

        return builder;
    }

    public static IHostApplicationBuilder AddRepositories(this IHostApplicationBuilder builder)
    {
        // Add GenericRepository<T> itself
        builder.Services.AddScoped(typeof(GenericRepository<>));

        // Get the assembly containing the repositories
        var repositoryAssembly = typeof(GenericRepository<>).Assembly;

        // Register all classes in the specified namespace that inherit from GenericRepository<T>
        foreach (var type in repositoryAssembly.GetTypes())
        {
            if (type is { IsClass: true, IsAbstract: false, Namespace: "Microsoft.Greenlight.Shared.Repositories" })
            {
                var baseType = type.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(GenericRepository<>))
                    {
                        builder.Services.AddScoped(type);
                        break;
                    }
                    baseType = baseType.BaseType;
                }
            }
        }

        return builder;
    }
}
