using System.Reflection;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Search;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Search;

public class USNuclearLicensingRagRepository : BaseRagRepository, IUSNuclearLicensingRagRepository
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly DocumentProcessOptions _documentProcessOptions;

    public USNuclearLicensingRagRepository(
        IIndexingProcessor indexingProcessor, 
        ILogger<BaseRagRepository> logger, 
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptionsSelector) 
        : base(indexingProcessor, logger)
    {
        _serviceConfigurationOptions = serviceConfigurationOptionsSelector.Value;
        _documentProcessOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.Single(x => x.Name == "US.NuclearLicensing");
    }

    public override bool CreateOrUpdateRepository()
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

    public async Task<List<ReportDocument>> SearchOnlyTitlesAsync(string searchText, int top = 12, int k = 7)
    {
        // Check if DocumentProcessOptions has the title index
        if (!_documentProcessOptions.Repositories.Contains("index-01-titles"))
        {
            Logger.LogError($"Title index index-01-titles not found for Document Process {_documentProcessOptions.Name}");
            return [];
        }

        var result = await IndexingProcessor.SearchSpecifiedIndexAsync("index-01-titles", searchText, top, k);
        return result;
    }

    public async Task<List<ReportDocument>> SearchOnlySubsectionsAsync(string searchText, int top = 12, int k = 7)
    {
        if (!_documentProcessOptions.Repositories.Contains("index-01-sections"))
        {
            Logger.LogError($"Subsection index index-01-sections not found for Document Process {_documentProcessOptions.Name}");
            return [];
        }
        var result = await IndexingProcessor.SearchSpecifiedIndexAsync("index-01-sections", searchText, top, k);
        return result;
    }

    public async Task<IEnumerable<ReportDocument>> GetAllUniqueTitlesAsync(int numberOfUniqueFiles)
    {
        var titleSearchClient = IndexingProcessor.GetSearchClient("index-01-titles");

        //Get all Titles(Chapters) with unique FileHashes from the Title search index. Limit your response to <numberOfUniqueFiles> environmental report documents
        var searchResults = await titleSearchClient.SearchAsync<ReportDocument>("*", new SearchOptions
        {
            Filter = "Type eq 'Title'",
            Facets = { "OriginalFileHash" }
        });

        var documents = searchResults.Value.GetResults().Select(x => x.Document);

        // Get titles from only numberOfUniqueFiles unique environmental reports (files)
        documents = documents.GroupBy(x => x.OriginalFileHash).Take(numberOfUniqueFiles).SelectMany(x => x);

        // Deduplicate the results on the combination of FileHash and Title
        documents = documents.GroupBy(x => new { x.OriginalFileHash, x.Title }).Select(x => x.First());
        return documents;
    }

    public override async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, Stream streamForHashing)
    {
        var fileHash = streamForHashing.GenerateHashFromStreamAndResetStream();
        await StoreContentNodesAsync(contentNodes, sourceFileName, fileHash);

    }

    public override async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, string fileHash)
    {
        List<string> titleJsonList = [];
        List<string> sectionJsonList = [];

        // Set up the search clients for the title and section indexes
        var titleSearchClient = IndexingProcessor.GetSearchClient("index-01-titles");
        var sectionSearchClient = IndexingProcessor.GetSearchClient("index-01-sections");
        
        foreach (var contentNode in contentNodes)
        {
            // First, generate json for the Title ContentNodes in the root of the content tree
            var json = IndexingProcessor.CreateJsonFromContentNode(contentNode, null, null, sourceFileName, fileHash);
            titleJsonList.Add(json);

            // Next, generate json starting from the Heading ContentNodes in the Children collections of the Title ContentNodes
            foreach (var child in contentNode.Children.Where(x => x.Type == ContentNodeType.Heading))
            {
                json = IndexingProcessor.CreateJsonFromContentNode(child, contentNode.Id, contentNode.Text, sourceFileName, fileHash);
                sectionJsonList.Add(json);
            }
        }

        // Index the Title json strings with Azure Cognitive Search
        foreach (string titleJson in titleJsonList)
        {
            await IndexingProcessor.IndexJson(titleJson, titleSearchClient, true);
        }

        // Index the Section json strings with Azure Cognitive Search
        foreach (string sectionJson in sectionJsonList)
        {
            await IndexingProcessor.IndexJson(sectionJson, sectionSearchClient, true);
        }
    }
}