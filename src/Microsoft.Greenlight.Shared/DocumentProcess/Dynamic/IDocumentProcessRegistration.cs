using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;

public interface IDocumentProcessRegistration
{
    string ProcessName { get; }
    IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options);
}
