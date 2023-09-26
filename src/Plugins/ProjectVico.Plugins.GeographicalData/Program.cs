using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Plugins.GeographicalData.Connectors;
using ProjectVico.Plugins.Shared.Extensions;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x => x.BuildPluginConfigurationBuilder());

hostBuilder.ConfigureServices((hostContext, services) =>
{
    services.AddScoped<IMappingConnector, AzureMapsConnector>();
});


var host = hostBuilder.Build();
host.Run();
