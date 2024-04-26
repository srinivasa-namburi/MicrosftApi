using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using ProjectVico.V2.Shared.Configuration;

namespace ProjectVico.V2.DocumentProcess.Shared.Search;

public class KernelMemoryRepository : IKernelMemoryRepository
{
    private IKernelMemory? _memory;
    private readonly IServiceProvider _sp;
    private readonly ILogger<KernelMemoryRepository> _logger;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public KernelMemoryRepository(
        IServiceProvider sp,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        ILogger<KernelMemoryRepository> logger)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _sp = sp;
        _logger = logger;
    }


    public async Task StoreContentAsync(string documentProcessName, string indexName, Stream fileStream,
        string fileName, string? documentUrl, string? userId = null)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
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

        await _memory.ImportDocumentAsync(documentRequest);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(DocumentProcessOptions documentProcessOptions, string searchText, int top = 12, double minRelevance = 0.7)
    {
        var documentProcessName = documentProcessOptions.Name;
        return await SearchAsync(documentProcessName, searchText, top, minRelevance);
    }

    public async Task<List<SortedDictionary<int, Citation.Partition>>> SearchAsync(string documentProcessName, string searchText, int top = 12, double minRelevance = 0.7)
    {
        GetKernelMemoryForDocumentProcess(documentProcessName);

        if (_memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Process {DocumentProcessName}", documentProcessName);
            throw new Exception("Kernel Memory service not found for Document Process " + documentProcessName);
        }

        var documentProcess = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.FirstOrDefault(x => x.Name == documentProcessName);
        if (documentProcess == null)
        {
            _logger.LogError("Document Process {DocumentProcessName} not found in configuration", documentProcessName);
            throw new Exception("Document Process " + documentProcessName + " not found in configuration");
        }

        // Search the kernel memory for the given searchText.

        var results = await _memory.SearchAsync(searchText,
            index: documentProcess.Repositories[0],
            minRelevance: minRelevance,
            limit: top);

        var sortedPartitions = new List<SortedDictionary<int, Citation.Partition>>();

        foreach (var citation in results.Results)
        {
            foreach (var partition in citation.Partitions)
            {
                // Collect partitions in a sorted collection
                var partitions = new SortedDictionary<int, Citation.Partition> { [partition.PartitionNumber] = partition };

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

                // Adds the sorted dictionary of partitions for this result to the sortedPartitions list

                sortedPartitions.Add(partitions);
            }
        }
        return sortedPartitions;
    }

    private void GetKernelMemoryForDocumentProcess(string documentProcessName)
    {
        if (_memory != null) return;

        var scope = _sp.CreateScope();
        _memory = scope.ServiceProvider.GetKeyedService<IKernelMemory>(documentProcessName + "-IKernelMemory");
    }
}