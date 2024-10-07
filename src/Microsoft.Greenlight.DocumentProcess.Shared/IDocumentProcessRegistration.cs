using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.DocumentProcess.Shared;

public interface IDocumentProcessRegistration
{
    string ProcessName { get; }
    IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options);
}
