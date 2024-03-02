using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.Shared.Extensions;

public static class BuilderExtensions
{
    public static IHostApplicationBuilder AddPluggableEndpointDefinitions(this IHostApplicationBuilder builder)
    {
        var endpointTypes = GetEndpointTypes();
        foreach (var endpointType in endpointTypes)
        {
            builder.Services.AddScoped(typeof(IEndpointDefinition), endpointType);
        }

        return builder;
    }

    public static WebApplication MapPluggableEndpointDefinitions(this WebApplication app)
    {
        List<IEndpointDefinition> pluginEndpoints;
        using (var scope = app.Services.CreateScope())
        {
            pluginEndpoints = scope.ServiceProvider.GetServices<IEndpointDefinition>().ToList();
            foreach (var plugin in pluginEndpoints)
            {
                plugin?.DefineEndpoints(app);
            }
        }

        return app;
    }

    private static List<Type> GetEndpointTypes()
    {
        var endpointDefinitionType = typeof(IEndpointDefinition);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var endpointTypes = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => endpointDefinitionType.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

        return endpointTypes.ToList();
    }
}