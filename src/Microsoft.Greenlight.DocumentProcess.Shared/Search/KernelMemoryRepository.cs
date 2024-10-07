using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Context;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

public class KernelMemoryRepository : IKernelMemoryRepository
{
    private IKernelMemory? _memory;
    private readonly IServiceProvider _sp;
    private readonly ILogger<KernelMemoryRepository> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;


    public KernelMemoryRepository(
        IServiceProvider sp,
        ILogger<KernelMemoryRepository> logger,
        IDocumentProcessInfoService documentProcessInfoService)
    {
        _sp = sp;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
    }

    public async Task StoreContentAsync(string documentProcessName, string indexName, Stream fileStream,
        string fileName, string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        // URL encode the documentUrl if it is not null
        if (documentUrl != null)
        {
            documentUrl = WebUtility.UrlEncode(documentUrl);
        }

        var documentRequest = new DocumentUploadRequest()
        {
            DocumentId = fileName,
            Files = [new(fileName, fileStream)],
            Tags = new TagCollection()
            {
                "DocumentProcessName", documentProcessName,
                "OriginalDocumentUrl", documentUrl ?? string.Empty,
                "UploadedByUserOid", userId ?? string.Empty
            },
            Index = indexName
        };
        
        if (additionalTags != null)
        {
            // Check the keys in the additionalParameters dictionary. If any of them look like URLs (if they start with https: or http:, URL encode the value
            foreach (var (key, value) in additionalTags)
            {
                if (key.StartsWith("http:") || key.StartsWith("https:"))
                {
                    additionalTags[key] = WebUtility.UrlEncode(value);
                }
            }


            foreach (var (key, value) in additionalTags)
            {
                documentRequest.Tags.Add(key, value);
            }
        }

        await _memory.ImportDocumentAsync(documentRequest);
    }

    public async Task DeleteContentAsync(string documentProcessName, string indexName, string fileName)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        await _memory.DeleteDocumentAsync(fileName, indexName);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessInfo documentProcessInfo, string searchText, int top = 12, double minRelevance = 0.7)
    {
        var documentProcessName = documentProcessInfo.ShortName;
        return await SearchAsync(documentProcessName, searchText, top, minRelevance);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessOptions documentProcessOptions, string searchText, int top = 12, double minRelevance = 0.7)
    {
        var documentProcessName = documentProcessOptions.Name;
        return await SearchAsync(documentProcessName, searchText, top, minRelevance);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string searchText, int top = 12, double minRelevance = 0.7)
    {
        var documentProcess =
            await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessName);

        if (documentProcess == null)
        {
            _logger.LogError("Document Process {DocumentProcessName} not found in configuration", documentProcessName);
            throw new Exception("Document Process " + documentProcessName + " not found in configuration");
        }

        var indexName = documentProcess.Repositories[0];
        var searchResults = await SearchAsync(documentProcessName, indexName, searchText, top, minRelevance);

        return searchResults;
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName,
        string indexName, string searchText, int top = 12, double minRelevance = 0.7)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        var results = await _memory.SearchAsync(searchText,
            index: indexName,
            minRelevance: minRelevance,
            limit: top);

        var sortedPartitions = new List<SortedDictionary<int, Citation.Partition>>();

        foreach (var citation in results.Results)
        {
            foreach (var partition in citation.Partitions)
            {
                // Collect partitions in a sorted collection
                var partitions = new SortedDictionary<int, Citation.Partition> { [partition.PartitionNumber] = partition };

                #region Adjacent partition fetching - currently inactive
                //TODO:Get adjacent partitions. The below code isn't operational because it sometimes fails as there is no previous or next partition. Needs to be more resilient to that.

                // //Filters to fetch adjacent partitions
                //var filters = new List<MemoryFilter>
                //{

                //     MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber - 1}"),
                //     MemoryFilters.ByDocument(citation.DocumentId).ByTag(Constants.ReservedFilePartitionNumberTag, $"{partition.PartitionNumber + 1}")
                //};

                // // Fetch adjacent partitions and add them to the sorted collection

                // SearchResult adjacentList = await _memory.SearchAsync("", filters: filters, limit: 2);

                // // Make this more resilient to failures when there is no previous or next partition

                // foreach (var adjacent in adjacentList.Results.First().Partitions)
                // {
                //     partitions[adjacent.PartitionNumber] = adjacent;
                // }

                #endregion

                // Adds the sorted dictionary of partitions for this result to the sortedPartitions list
                sortedPartitions.Add(partitions);
            }
        }
        return sortedPartitions;
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string indexName, Dictionary<string, string> parametersExactMatch, string searchText, int top = 12,
        double minRelevance = 0.7)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        var tagFilter = new List<MemoryFilter>();
        foreach (var parameter in parametersExactMatch)
        {
            tagFilter.Add(new MemoryFilter().ByTag(parameter.Key, parameter.Value));
        }

        var searchResult = await _memory.SearchAsync(searchText,
            index: indexName,
            filters: tagFilter, 
            limit: top, 
            minRelevance: minRelevance);

        var sortedPartitions = new List<SortedDictionary<int, Citation.Partition>>();

        foreach (var citation in searchResult.Results)
        {
            foreach (var partition in citation.Partitions)
            {
                // Collect partitions in a sorted collection
                var partitions = new SortedDictionary<int, Citation.Partition> { [partition.PartitionNumber] = partition };
                // Adds the sorted dictionary of partitions for this result to the sortedPartitions list
                sortedPartitions.Add(partitions);
            }
        }
        return sortedPartitions;
    }

    public async Task<MemoryAnswer?> AskAsync(string documentProcessName, string indexName, Dictionary<string, string> parametersExactMatch, string question)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        var tagFilter = new List<MemoryFilter>();
        foreach (var parameter in parametersExactMatch)
        {
            tagFilter.Add(new MemoryFilter().ByTag(parameter.Key, parameter.Value));
        }

        var result = await _memory.AskAsync(question, indexName, filters: tagFilter, minRelevance:0.7D);
        return result;
    }

    private void GetKernelMemoryForDocumentProcess(string documentProcessName)
    {
        if (_memory != null) return;
        _memory = _sp.GetRequiredServiceForDocumentProcess<IKernelMemory>(documentProcessName);
    }
}
