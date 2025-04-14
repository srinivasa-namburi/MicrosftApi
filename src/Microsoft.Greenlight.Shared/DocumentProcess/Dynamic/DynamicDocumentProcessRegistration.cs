using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Shared.Configuration;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Dynamic;

public class DynamicDocumentProcessRegistration : IDocumentProcessRegistration
{
    public string ProcessName => "Dynamic";

    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        // Services registered individually for ALL dynamic document processes
        return builder;
    }
}