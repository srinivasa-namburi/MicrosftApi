using Microsoft.Extensions.Logging;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Search;

public abstract class BaseRagRepository : IRagRepository, IBaseRagRepository
{
    protected readonly IIndexingProcessor IndexingProcessor;
    protected readonly ILogger<BaseRagRepository> Logger;

    public BaseRagRepository(IIndexingProcessor indexingProcessor, ILogger<BaseRagRepository> logger)
    {
        IndexingProcessor = indexingProcessor;
        Logger = logger;
    }

    public abstract bool CreateOrUpdateRepository();
    public abstract bool ClearRepositoryContent();

    public abstract Task<List<RagRepositorySearchResult>> SearchAsync(string searchText, int top = 12, int k = 7);
    
    public async Task<List<RagRepositorySearchResult>> SearchAsync(DocumentProcessOptions documentProcessOptions,
        string searchText, int top = 12, int k = 7)
    {
        var results = new List<RagRepositorySearchResult>();
        foreach (var repository in documentProcessOptions.Repositories)
        {
            var resultDocuments = await IndexingProcessor.SearchSpecifiedIndexAsync(repository, searchText, top, k);
            var result = new RagRepositorySearchResult()
            {
                RepositoryName = repository,
                Documents = resultDocuments
            };
            results.Add(result);
        }

        return results;
    }

    public async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName,
        Stream streamForHashing)
    {
        await IndexingProcessor.IndexAndStoreContentNodesAsync(contentNodes, sourceFileName, streamForHashing);
    }

    public async Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, string fileHash)
    {
        await IndexingProcessor.IndexAndStoreContentNodesAsync(contentNodes, sourceFileName, fileHash);
    }


}