using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Plugins.GeographicalData.Connectors;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(service =>
    {
        service.AddTransient<IMappingConnector, AzureMapsConnector>();
    })
    .Build();

host.Run();
