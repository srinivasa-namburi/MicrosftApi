using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Plugins.GeographicalData.Connectors;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("local.settings.json", optional:true)
    .AddEnvironmentVariables()
    .Build();

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x=>x.AddConfiguration(config));

var azureMapsKey = config.GetValue<string>("AzureMapsKey");

hostBuilder.ConfigureServices(service =>
{
    service.AddTransient<IMappingConnector>(x =>

        ActivatorUtilities.CreateInstance<AzureMapsConnector>(x, azureMapsKey));
});

var host = hostBuilder.Build();
host.Run();


