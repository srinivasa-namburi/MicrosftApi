using Microsoft.Extensions.Hosting;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.Dynamic;

public class DynamicDocumentProcessRegistration : IDocumentProcessRegistration
{
    // TODO : Retrieve the Document Process Name from the service
    public string ProcessName => "Dynamic";
    public IHostApplicationBuilder RegisterDocumentProcess(IHostApplicationBuilder builder, ServiceConfigurationOptions options)
    {
        // Shared / System services


        // END Shared / System services


        
        return builder;
    }
}