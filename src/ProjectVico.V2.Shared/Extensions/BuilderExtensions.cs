using Aspire.Azure.Messaging.ServiceBus;
using Aspire.Azure.Search.Documents;
using Aspire.Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Services.Search;

namespace ProjectVico.V2.Shared.Extensions;

public static class BuilderExtensions
{

    public static IHostApplicationBuilder AddProjectVicoServices(this IHostApplicationBuilder builder, AzureCredentialHelper credentialHelper, ServiceConfigurationOptions serviceConfigurationOptions)
    {
        // Common services and dependencies
        builder.AddAzureServiceBusClient("sbus", configureSettings: delegate (AzureMessagingServiceBusSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureSearchClient("aiSearch", configureSettings: delegate(AzureSearchSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        builder.AddAzureBlobClient("blob-docing", configureSettings: delegate (AzureStorageBlobsSettings settings)
        {
            settings.Credential = credentialHelper.GetAzureCredential();
        });

        // These services use key-based authentication and don't need to use the AzureCredentialHelper.
        builder.AddKeyedAzureOpenAIClient("openai-planner");
        builder.AddRabbitMQClient("rabbitmqdocgen");
        builder.AddRedisClient("redis");

        builder.Services.AddScoped<AzureFileHelper>();
        builder.Services.AddSingleton<SearchClientFactory>();

        builder.AddDocGenDbContext(serviceConfigurationOptions);
        return builder;

    }

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