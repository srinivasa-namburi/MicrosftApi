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
    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        var docDbContext = serviceProvider.GetRequiredService<DocGenerationDbContext>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var dynamicDocumentProcessDefinitions = docDbContext.DynamicDocumentProcessDefinitions
            .Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory)
            .Include(x=>x.DocumentOutline)
            .ToList();

        var mapper = serviceProvider.GetService<IMapper>();

        foreach (var documentProcess in dynamicDocumentProcessDefinitions)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);

            serviceCollection.AddKeyedSingleton<KmDocsPlugin>(documentProcess.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo));
        }

        var serviceConfigurationOptions = configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;
        
        foreach (var staticDocumentProcess in serviceConfigurationOptions.GreenlightServices.DocumentProcesses)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(staticDocumentProcess);
            serviceCollection.AddKeyedSingleton<KmDocsPlugin>(documentProcessInfo.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo));
        }
    }
}
