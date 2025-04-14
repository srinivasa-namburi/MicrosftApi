using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Search;

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
    public abstract Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName,
        Stream streamForHashing);
    public abstract Task StoreContentNodesAsync(List<ContentNode> contentNodes, string sourceFileName, string fileHash);
    
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

   



}
