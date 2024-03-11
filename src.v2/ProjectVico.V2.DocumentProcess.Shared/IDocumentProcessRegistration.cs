using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.Shared;

public interface IDocumentProcessRegistration
{
    string ProcessName { get; }
    IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options);
}