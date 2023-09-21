using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Plugins.GeographicalData.Connectors;
using ProjectVico.Plugins.Shared.Extensions;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x => x.BuildPluginConfigurationBuilder());

hostBuilder.ConfigureServices((hostContext, services) =>
{
    services.AddTransient<IMappingConnector>(x =>
        ActivatorUtilities.CreateInstance<AzureMapsConnector>(x, hostContext.Configuration.GetValue<string>("AzureMapsKey"))
        );
});

var host = hostBuilder.Build();
host.Run();
