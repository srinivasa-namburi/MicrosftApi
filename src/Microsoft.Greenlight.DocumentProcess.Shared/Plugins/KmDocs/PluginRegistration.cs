using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Plugins.KmDocs;

public class PluginRegistration : IPluginRegistration
{
    public IHostApplicationBuilder RegisterPlugin(IHostApplicationBuilder builder)
    {
        var serviceProvider = builder.Services.BuildServiceProvider();

        var docDbContext = serviceProvider.GetRequiredService<DocGenerationDbContext>();

        var dynamicDocumentProcessDefinitions = docDbContext.DynamicDocumentProcessDefinitions
            .Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory)
            .Include(x=>x.DocumentOutline)
            .ToList();

        var mapper = serviceProvider.GetService<IMapper>();

        foreach (var documentProcess in dynamicDocumentProcessDefinitions)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
            builder.Services.AddKeyedSingleton<KmDocsPlugin>(documentProcess.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo));
        }

        var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;
        
        foreach (var staticDocumentProcess in serviceConfigurationOptions.GreenlightServices.DocumentProcesses)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(staticDocumentProcess);
            builder.Services.AddKeyedSingleton<KmDocsPlugin>(documentProcessInfo.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo));
        }

        return builder;
    }
}
