using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Interfaces;

namespace ProjectVico.V2.DocumentProcess.Shared.Plugins.KmDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();

        var docDbContext = serviceProvider.GetRequiredService<DocGenerationDbContext>();

        var processes = docDbContext.DynamicDocumentProcessDefinitions.Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();

        var mapper = serviceProvider.GetService<IMapper>();

        foreach (var documentProcess in processes)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
            builder.Services.AddKeyedSingleton<KmDocsPlugin>(documentProcess.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo));
        }

        return builder;
    }
}