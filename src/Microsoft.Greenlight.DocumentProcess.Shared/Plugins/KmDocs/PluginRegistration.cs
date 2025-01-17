using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Extensions.Plugins;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Plugins.KmDocs;

public class PluginRegistration : IPluginRegistration
{
    private readonly ConsolidatedSearchOptions _defaultSearchOptions = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                IndexName = "default",
                Top = 5,
                MinRelevance = 0.7,
                PrecedingPartitionCount = 0,
                FollowingPartitionCount = 0
            };

    public void RegisterPlugin(IServiceCollection serviceCollection, IServiceProvider serviceProvider)
    {
        var docDbContext = serviceProvider.GetRequiredService<DocGenerationDbContext>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
       
        var dynamicDocumentProcessDefinitions = docDbContext.DynamicDocumentProcessDefinitions
            .Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory)
            .Include(x=>x.DocumentOutline)
            .ToList();

        var mapper = serviceProvider.GetRequiredService<IMapper>();

        foreach (var documentProcess in dynamicDocumentProcessDefinitions)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
            // We can't use IConsolidatedSearchOptionsFactory here because it's not registered in the service collection yet
            var consolidatedSearchOptions = GetConsolidatedSearchOptionsForDocumentProcess(documentProcessInfo);

            serviceCollection.AddKeyedSingleton<KmDocsPlugin>(documentProcess.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo, consolidatedSearchOptions));
        }

        var serviceConfigurationOptions = configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;
        
        foreach (var staticDocumentProcess in serviceConfigurationOptions.GreenlightServices.DocumentProcesses)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(staticDocumentProcess);

            // We can't use IConsolidatedSearchOptionsFactory here because it's not registered in the service collection yet
            var consolidatedSearchOptions = GetConsolidatedSearchOptionsForDocumentProcess(documentProcessInfo);
            
            serviceCollection.AddKeyedSingleton<KmDocsPlugin>(documentProcessInfo.ShortName + "-KmDocsPlugin",
                (provider, o) => new KmDocsPlugin(provider, documentProcessInfo, consolidatedSearchOptions));
        }
    }

    private ConsolidatedSearchOptions GetConsolidatedSearchOptionsForDocumentProcess(
        DocumentProcessInfo documentProcess)
    {
        var consolidatedSearchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
            IndexName = documentProcess.Repositories.FirstOrDefault() ?? "default",
            Top = documentProcess.NumberOfCitationsToGetFromRepository,
            MinRelevance = documentProcess.MinimumRelevanceForCitations,
            PrecedingPartitionCount = documentProcess.PrecedingSearchPartitionInclusionCount,
            FollowingPartitionCount = documentProcess.FollowingSearchPartitionInclusionCount
        };

        if (consolidatedSearchOptions.Top == 0 || consolidatedSearchOptions.Top >= 5)
        {
            consolidatedSearchOptions.Top = _defaultSearchOptions.Top;
        }
        if (consolidatedSearchOptions.MinRelevance == 0)
        {
            consolidatedSearchOptions.MinRelevance = _defaultSearchOptions.MinRelevance;
        }

        return consolidatedSearchOptions;
    }
}
