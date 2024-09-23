using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using ProjectVico.V2.Shared.Data;
using ProjectVico.V2.Shared.Repositories;

namespace ProjectVico.V2.Shared.Extensions;

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
        });

        builder.EnrichSqlServerDbContext<DocGenerationDbContext>(settings =>
        {
            settings.DisableRetry = false;
            settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
                        
        });

        builder.AddRepositories();

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
            if (type is { IsClass: true, IsAbstract: false, Namespace: "ProjectVico.V2.Shared.Repositories" })
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