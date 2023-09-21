using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.Plugins.DocQnA.Options;
using ProjectVico.Plugins.Shared.Extensions;

var hostBuilder = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostConfiguration(x => x.BuildPluginConfigurationBuilder());

hostBuilder.ConfigureServices((hostContext, services) =>
{
    services.Configure<AiOptions>(hostContext.Configuration.GetSection("AI"));
});

var host = hostBuilder.Build();
host.Run();
