using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.KernelMemory;
using System.Net;

namespace Microsoft.Greenlight.Shared.Services.Search;

public class KernelMemoryRepository : IKernelMemoryRepository
{
    private readonly IKernelMemoryInstanceFactory _kernelMemoryInstanceFactory;
    private IKernelMemory? _memory;
    private readonly IServiceProvider _sp;
    private readonly ILogger<KernelMemoryRepository> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IDocumentLibraryInfoService _documentLibraryInfoService;

    public KernelMemoryRepository(
        IServiceProvider sp,
        ILogger<KernelMemoryRepository> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IKernelMemoryInstanceFactory kernelMemoryInstanceFactory
    )
    {
        _sp = sp;
        _logger = logger;
        _documentProcessInfoService = documentProcessInfoService;
        _documentLibraryInfoService = documentLibraryInfoService;
        _kernelMemoryInstanceFactory = kernelMemoryInstanceFactory;
    }

    public async Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream,
        string fileName, string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null)
    {
        var memory = await GetKernelMemoryForDocumentLibrary(documentLibraryName);

        if (memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Library {DocumentLibraryName}", documentLibraryName);
            throw new Exception("Kernel Memory service not found for Document Library " + documentLibraryName);
        }

        // URL encode the documentUrl if it is not null
        if (documentUrl != null)
        {
            documentUrl = WebUtility.UrlEncode(documentUrl);
        }

        // Decode the fileName
        fileName = WebUtility.UrlDecode(fileName);

        // Sanitize the fileName - replace spaces with underscores, pluses with underscores, tildes with underscores, and slashes with underscores
        fileName = fileName.Replace(" ", "_").Replace("+", "_").Replace("~", "_").Replace("/", "_");

        var documentRequest = new DocumentUploadRequest()
        {
            DocumentId = fileName,
            Files = [new DocumentUploadRequest.UploadedFile(fileName, fileStream)],
            Index = indexName
        };

        var isDocumentLibraryDocument = documentLibraryName.StartsWith("Additional-").ToString().ToLowerInvariant();
        var tags = new Dictionary<string, string>
        {
            {"DocumentProcessName", documentLibraryName},
            {"IsDocumentLibraryDocument", isDocumentLibraryDocument},
            {"OriginalDocumentUrl", documentUrl ?? string.Empty},
            {"UploadedByUserOid", userId ?? string.Empty}
        };

        foreach (var (key, value) in tags)
        {
            documentRequest.Tags.Add(key, value);
        }

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

        await memory.ImportDocumentAsync(documentRequest);
    }

    public async Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName)
    {
        var memory = await GetKernelMemoryForDocumentLibrary(documentLibraryName);

        if (memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Library {DocumentLibraryName}", documentLibraryName);
            throw new Exception("Kernel Memory service not found for Document Library " + documentLibraryName);
        }

        await memory.DeleteDocumentAsync(fileName, indexName);
    }
   
    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(
    string documentLibraryName,
    string searchText,
    ConsolidatedSearchOptions options)
    {
        var memory = await GetKernelMemoryForDocumentLibrary(documentLibraryName);
        if (memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Library {DocumentLibraryName}", documentLibraryName);
            throw new Exception("Kernel Memory service not found for Document Library " + documentLibraryName);
        }

        // Retrieve search results
        var results = await GetSearchResultsAsync(
            memory,
            options.IndexName,
            options.ParametersExactMatch,
            searchText,
            options.MinRelevance,
            options.Top);

        // Determine if Document Library or Document Process, and set up factory accordingly
        Func<Citation, KernelMemoryDocumentSourceReferenceItem> sourceReferenceItemFactory;
        if (options.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
        {
            // Document Process
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryName);
            if (documentProcess == null)
            {
                _logger.LogError("Document Process {DocumentProcessName} not found in configuration", documentLibraryName);
                throw new Exception("Document Process " + documentLibraryName + " not found in configuration");
            }

            sourceReferenceItemFactory = citation => new DocumentProcessRepositorySourceReferenceItem
            {
                DocumentProcessShortName = documentLibraryName,
                IndexName = options.IndexName
            };
        }
        else if (options.DocumentLibraryType == DocumentLibraryType.Reviews)
        {
            sourceReferenceItemFactory = citation => new DocumentLibrarySourceReferenceItem
            {
                DocumentLibraryShortName = "Reviews", 
                IndexName = options.IndexName
            };
        }
        else
        {
            // Additional Document Library
            var internalDocumentLibraryName =
                documentLibraryName.Replace("Additional-", "", StringComparison.OrdinalIgnoreCase);

            var documentLibrary =
                await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(internalDocumentLibraryName);

            if (documentLibrary == null)
            {
                _logger.LogError("Document Library {DocumentLibraryName} not found in configuration",
                    internalDocumentLibraryName);
                throw new Exception("Document Library " + internalDocumentLibraryName + " not found in configuration");
            }

            sourceReferenceItemFactory = citation => new DocumentLibrarySourceReferenceItem
            {
                DocumentLibraryShortName = internalDocumentLibraryName,
                IndexName = options.IndexName
            };
        }

        // Create source reference items - this may include adjacent partitions depending on incoming options
        var sourceItems = await CreateSourceReferenceItemsAsync(
            results,
            memory,
            sourceReferenceItemFactory,
            options.PrecedingPartitionCount,
            options.FollowingPartitionCount);

        return sourceItems;
    }

    private async Task<SearchResult> GetSearchResultsAsync(
        IKernelMemory memory,
        string indexName,
        Dictionary<string, string> parametersExactMatch,
        string searchText,
        double minRelevance,
        int top)
    {
        if (parametersExactMatch.Count == 0)
        {
            return await memory.SearchAsync(searchText, index: indexName, minRelevance: minRelevance, limit: top);
        }

        var tagFilter = new List<MemoryFilter>();
        foreach (var parameter in parametersExactMatch)
        {
            tagFilter.Add(new MemoryFilter().ByTag(parameter.Key, parameter.Value));
        }

        return await memory.SearchAsync(searchText, index: indexName, minRelevance: minRelevance, limit: top, filters: tagFilter);
    }

    private async Task<List<KernelMemoryDocumentSourceReferenceItem>> CreateSourceReferenceItemsAsync(
        SearchResult results,
        IKernelMemory memory,
        Func<Citation, KernelMemoryDocumentSourceReferenceItem> sourceReferenceItemFactory,
        int precedingPartitionCount,
        int followingPartitionCount)
    {
        var sourceReferenceItems = new List<KernelMemoryDocumentSourceReferenceItem>();

        foreach (var citation in results.Results)
        {
            var sourceReferenceItem = sourceReferenceItemFactory(citation);
            sourceReferenceItem.SetBasicParameters();

            // Add adjacent partitions
            await AddAdjacentPartitionsAsync(memory, citation, precedingPartitionCount, followingPartitionCount);

            // Set source reference link if available
            var firstPartition = citation.Partitions.FirstOrDefault();
            if (firstPartition != null && firstPartition.Tags.ContainsKey("OriginalDocumentUrl"))
            {
                sourceReferenceItem.SourceReferenceLinkType = SourceReferenceLinkType.SystemNonProxiedUrl;
                if (firstPartition.Tags.TryGetValue("OriginalDocumentUrl", out var links) && links is { Count: > 0 })
                {
                    sourceReferenceItem.SourceReferenceLink = links.FirstOrDefault();
                }
            }

            // Fix items where relevance is -Infinity or Infinity
            foreach (var partition in citation.Partitions)
            {
                if (double.IsInfinity(partition.Relevance))
                {
                    partition.Relevance = 0.0F;
                }
            }

            sourceReferenceItem.AddCitation(citation);

            sourceReferenceItems.Add(sourceReferenceItem);
        }

        return sourceReferenceItems;
    }

    private async Task AddAdjacentPartitionsAsync(
        IKernelMemory memory,
        Citation citation,
        int precedingPartitionCount,
        int followingPartitionCount)
    {
        if (citation?.Partitions == null || citation.Partitions.Count == 0)
            return;

        if (precedingPartitionCount <= 0 && followingPartitionCount <= 0)
            return;

        var allPartitions = new List<Citation.Partition>(citation.Partitions);
        var adjacentPartitions = new List<Citation.Partition>();

        foreach (var partition in citation.Partitions)
        {
            var neighborsMemoryFilters = new List<MemoryFilter>();

            if (precedingPartitionCount > 0)
            {
                for (int i = 1; i <= precedingPartitionCount; i++)
                {
                    var precedingPartitionNumber = partition.PartitionNumber - i;

                    // Only add this filter if the preceding partition number is not already in the list of partitions
                    if (allPartitions.All(p => p.PartitionNumber != precedingPartitionNumber))
                    {
                        neighborsMemoryFilters.Add(
                            MemoryFilters.ByDocument(citation.DocumentId)
                                .ByTag(Constants.ReservedFilePartitionNumberTag,
                                    precedingPartitionNumber.ToString()));
                    }
                }
            }

            if (followingPartitionCount > 0)
            {
                for (int i = 1; i <= followingPartitionCount; i++)
                {
                    var followingPartitionNumber = partition.PartitionNumber + i;

                    // Only add this filter if the following partition number is not already in the list of partitions
                    if (allPartitions.All(p => p.PartitionNumber != followingPartitionNumber))
                    {
                        neighborsMemoryFilters.Add(
                            MemoryFilters.ByDocument(citation.DocumentId)
                                .ByTag(Constants.ReservedFilePartitionNumberTag,
                                    followingPartitionNumber.ToString()));
                    }
                }
            }

            if (!neighborsMemoryFilters.Any())
            {
                continue;
            }

            var adjacentList = await memory.SearchAsync("", index: citation.Index, filters: neighborsMemoryFilters, limit: precedingPartitionCount + followingPartitionCount);

            foreach (var adjacentCitation in adjacentList.Results)
            {
                // We don't have a relevance number for these partitions, so set it to the same as the original partition
                foreach (var adjacentPartition in adjacentCitation.Partitions)
                {
                    adjacentPartition.Relevance = partition.Relevance;
                }
                adjacentPartitions.AddRange(adjacentCitation.Partitions);
            }
        }

        // Add the adjacent partitions to the original partitions
        foreach (var partition in adjacentPartitions.Where(p => !allPartitions.Contains(p)))
        {
            allPartitions.Add(partition);
        }

        // Replace the original partitions with the new partitions which includes the adjacent partitions in addition to the original partitions
        citation.Partitions = allPartitions;


        // Order the partitions by partition number
        citation.Partitions = citation.Partitions.OrderBy(p => p.PartitionNumber).ToList();
    }



    public async Task<MemoryAnswer?> AskAsync(string documentLibraryName, string indexName, Dictionary<string, string>? parametersExactMatch, string question)
    {
        var memory = await GetKernelMemoryForDocumentLibrary(documentLibraryName);

        if (memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Library {DocumentLibraryName}", documentLibraryName);
            throw new Exception("Kernel Memory service not found for Document Library " + documentLibraryName);
        }

        var tagFilter = new List<MemoryFilter>();

        MemoryAnswer? result;

        if (parametersExactMatch != null)
        {
            foreach (var parameter in parametersExactMatch)
            {
                tagFilter.Add(new MemoryFilter().ByTag(parameter.Key, parameter.Value));
            }

            result = await memory.AskAsync(question, indexName, filters: tagFilter, minRelevance: 0.7D);
        }
        else
        {
            result = await memory.AskAsync(question, indexName, minRelevance: 0.7D);
        }
        return result;
    }

    private async Task<IKernelMemory> GetKernelMemoryForDocumentLibrary(string documentLibraryName)
    {
        if (documentLibraryName.StartsWith("Additional-"))
        {
            var internalDocumentLibraryName = documentLibraryName.Replace("Additional-", "");
            var memory = await _kernelMemoryInstanceFactory.GetKernelMemoryInstanceForDocumentLibrary(internalDocumentLibraryName);
            return memory;
        }
        else
        {
            // For standard Document Processes, we have an instance of this repository for each process, so the Kernel Memory Instance is set to a member variable
            if (_memory != null) return _memory;
            _memory = _sp.GetServiceForDocumentProcess<IKernelMemory>(documentLibraryName);

            if (_memory == null)
            {
                _logger.LogError("Kernel Memory service not found for Document Process {DocumentLibraryName}", documentLibraryName);
                throw new Exception("Kernel Memory service not found for Document Process " + documentLibraryName);
            }

            return _memory;
        }
    }
}