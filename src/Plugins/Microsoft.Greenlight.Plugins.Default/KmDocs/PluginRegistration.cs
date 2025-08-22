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
using Microsoft.Greenlight.Shared.Plugins;

namespace Microsoft.Greenlight.Plugins.Default.KmDocs;

/// <summary>
/// This particular Plugin Registration doesn't use registration, only initialization, because it creates
/// many instances of the same plugin type with different configurations.
/// </summary>
public class PluginRegistration : IPluginInitializer
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

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var scopedServiceProvider = scope.ServiceProvider;
        var scopeFactory = scopedServiceProvider.GetRequiredService<IServiceScopeFactory>();
        // Resolve required services from the built container.

        var dbContext = await scopedServiceProvider
            .GetRequiredService<IDbContextFactory<DocGenerationDbContext>>()
            .CreateDbContextAsync();
        
        var configuration = scopedServiceProvider.GetRequiredService<IConfiguration>();
        var mapper = scopedServiceProvider.GetRequiredService<IMapper>();
        var registry = scopedServiceProvider.GetRequiredService<IPluginRegistry>();

        // For static plugins, query configuration if needed.
        var serviceConfigurationOptions = configuration
            .GetSection(ServiceConfigurationOptions.PropertyName)
            .Get<ServiceConfigurationOptions>()!;

        // Process static document processes from configuration.
        foreach (var staticDocumentProcess in serviceConfigurationOptions.GreenlightServices.DocumentProcesses)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(staticDocumentProcess);
            var consolidatedSearchOptions = GetConsolidatedSearchOptionsForDocumentProcess(documentProcessInfo);
            var pluginInstance = new UniversalDocsPlugin(scopeFactory, documentProcessInfo, consolidatedSearchOptions);

            registry.AddPlugin(documentProcessInfo.ShortName + "-UniversalDocsPlugin", pluginInstance, isDynamic: false);
        }

        var dynamicDocumentProcessDefinitions = dbContext.DynamicDocumentProcessDefinitions
            .Include(x => x.DocumentOutline)
            .ToList();

        foreach (var documentProcess in dynamicDocumentProcessDefinitions)
        {
            var documentProcessInfo = mapper.Map<DocumentProcessInfo>(documentProcess);
            var consolidatedSearchOptions = GetConsolidatedSearchOptionsForDocumentProcess(documentProcessInfo);

            var pluginInstance = new UniversalDocsPlugin(scopeFactory, documentProcessInfo, consolidatedSearchOptions);

            registry.AddPlugin(documentProcessInfo.ShortName + "-UniversalDocsPlugin", pluginInstance, isDynamic: false);
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
