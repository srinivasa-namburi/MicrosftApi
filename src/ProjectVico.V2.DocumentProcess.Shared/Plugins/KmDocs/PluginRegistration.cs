using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Services;

namespace ProjectVico.V2.DocumentProcess.Shared.Plugins.KmDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {

        var serviceProvider = builder.Services.BuildServiceProvider();

        var documentProcessService = serviceProvider.GetService<IDocumentProcessInfoService>();
        if (documentProcessService == null)
        {
            throw new InvalidOperationException("Document process info service not found");
        }

        var processes = documentProcessService.GetCombinedDocumentProcessInfoListAsync().GetAwaiter().GetResult();

        foreach (var documentProcess in processes.Where(documentProcess => documentProcess.LogicType == DocumentProcessLogicType.KernelMemory))
        {
            builder.Services.AddKeyedSingleton<KmDocsPlugin>(documentProcess.ShortName+"-KmDocsPlugin", (provider, o) => new KmDocsPlugin(provider, documentProcess));
        }

        return builder;
    }
}