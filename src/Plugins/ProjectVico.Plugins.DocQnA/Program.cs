using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run(); 
