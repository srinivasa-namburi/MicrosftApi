using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Belgium.NuclearLicensing.DSAR.Search;

public class BelgiumNuclearLicensingDSARRagRepository : BaseRagRepository, IRagRepository
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly DocumentProcessOptions _documentProcessOptions;

    public BelgiumNuclearLicensingDSARRagRepository(
        IIndexingProcessor indexingProcessor, 
        ILogger<BelgiumNuclearLicensingDSARRagRepository> logger, 
        IOptionsSnapshot<ServiceConfigurationOptions> serviceConfigurationOptionsSelector) 
        : base(indexingProcessor, logger)
    {
        _serviceConfigurationOptions = serviceConfigurationOptionsSelector.Value;
        _documentProcessOptions = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses.Single(x => x.Name == "Belgium.NuclearLicensing.DSAR");
        CreateOrUpdateRepository();
    }

    public sealed override bool CreateOrUpdateRepository()
    {
        foreach (var repository in _documentProcessOptions.Repositories)
        {
            try
            {
                IndexingProcessor.CreateOrUpdateIndex(repository);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Error creating or updating index {repository} for Document Process {_documentProcessOptions.Name}");
                return false;
            }
        }
        
        return true;
    }

    public override bool ClearRepositoryContent()
    {
        foreach (var repository in _documentProcessOptions.Repositories)
        {
            try
            {
                IndexingProcessor.DeleteIndex(repository);
            }
            catch (Exception e)
            {
                Logger.LogError(e,
                    $"Error deleting index {repository} for Document Process {_documentProcessOptions.Name}");
                return false;
            }
        }
        return true;
    }

    public override Task<List<RagRepositorySearchResult>> SearchAsync(string searchText, int top = 12, int k = 7)
    {
        return base.SearchAsync(_documentProcessOptions, searchText, top, k);
    }

    public override async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, Stream streamForHashing)
    {
        var fileHash = streamForHashing.GenerateHashFromStreamAndResetStream();
        await StoreContentNodesAsync(contentNodes, sourceFileName, fileHash);
    }

    public override async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, string fileHash)
    {
        List<string> sectionJsonList = [];
        
        // Set up the search client for the index
        var sectionSearchClient = IndexingProcessor.GetSearchClient("index-belgium-dsar-sections");
        
        foreach (var contentNode in contentNodes)
        {
            // Generate json starting from the Heading ContentNodes in the Children collections of the Title ContentNodes
            foreach (var child in contentNode.Children.Where(x => x.Type == ContentNodeType.Heading))
            {
                var json = IndexingProcessor.CreateJsonFromContentNode(child, contentNode.Id, contentNode.Text, sourceFileName, fileHash);
                sectionJsonList.Add(json);
            }
        }
        
        // Index the Section json strings with Azure Cognitive Search
        foreach (string sectionJson in sectionJsonList)
        {
            await IndexingProcessor.IndexJson(sectionJson, sectionSearchClient, true);
        }
    }
}
