// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Plugins.Default.GeographicalData.Connectors;
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Plugins.Default.GeographicalData;

public class PluginRegistration : IPluginRegistration
{
    public void RegisterPlugin(IServiceCollection serviceCollection)
    {
        // Register a conditional mapping connector that falls back to a NoOp implementation
        // when the Azure Maps API key is not configured. This prevents plugin initialization
        // failures at startup on developer machines without keys, while providing
        // a clear runtime message if the plugin features are invoked.
        serviceCollection.AddTransient<IMappingConnector>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var apiKey = configuration["ServiceConfiguration:AzureMaps:Key"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var logger = sp.GetRequiredService<ILogger<NoOpMappingConnector>>();
                return new NoOpMappingConnector(logger);
            }

            // Pass the validated key directly to the connector to avoid re-reading
            // from configuration and accidentally constructing with a null/empty value.
            return new AzureMapsConnector(apiKey);
        });
        serviceCollection.AddTransient<FacilitiesPlugin>();
    }

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();
        registry.AddPlugin("FacilitiesPlugin", serviceProvider.GetRequiredService<FacilitiesPlugin>(), isDynamic: false);
    }
}
