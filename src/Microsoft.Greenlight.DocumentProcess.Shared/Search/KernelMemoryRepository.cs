using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.KernelMemory;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Search;

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
        int top = 12, 
        double minRelevance = 0.7)
    {
        string indexName = string.Empty;
        if(documentLibraryName.StartsWith("Additional-"))
        {
            var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(documentLibraryName);
            if (documentLibrary == null)
            {
                _logger.LogError("Document Library {DocumentLibraryName} not found in configuration", documentLibraryName);
                throw new Exception("Document Library " + documentLibraryName + " not found in configuration");
            }

            indexName = documentLibrary.IndexName;

        }
        else
        {
            var documentProcess =
                await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryName);

            if (documentProcess == null)
            {
                _logger.LogError("Document Process {DocumentProcessName} not found in configuration", documentLibraryName);
                throw new Exception("Document Process " + documentLibraryName + " not found in configuration");
            }

            indexName = documentProcess.Repositories[0];
        }

        var searchResults = await SearchAsync(documentLibraryName, indexName, searchText, top, minRelevance);

        return searchResults;
    }

    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(
        string documentLibraryName,
        string indexName, 
        string searchText, 
        int top = 12, 
        double minRelevance = 0.7)
    {
        var blankParameters = new Dictionary<string, string>();
        return await SearchAsync(documentLibraryName, indexName, blankParameters, searchText, top, minRelevance);
    }

    public async Task<List<KernelMemoryDocumentSourceReferenceItem>> SearchAsync(
        string documentLibraryName, 
        string indexName, 
        Dictionary<string, string> parametersExactMatch, 
        string searchText, 
        int top = 12,
        double minRelevance = 0.7)
    {
        var memory = await GetKernelMemoryForDocumentLibrary(documentLibraryName);

        if (memory == null)
        {
            _logger.LogError("Kernel Memory service not found for Document Library {DocumentLibraryName}", documentLibraryName);
            throw new Exception("Kernel Memory service not found for Document Library " + documentLibraryName);
        }

        SearchResult results;
        if (parametersExactMatch.Count <= 0)
        {
            results = await memory.SearchAsync(searchText,
                index: indexName,
                minRelevance: minRelevance,
                limit: top);
        }
        else
        {
            var tagFilter = new List<MemoryFilter>();
            foreach (var parameter in parametersExactMatch)
            {
                tagFilter.Add(new MemoryFilter().ByTag(parameter.Key, parameter.Value));
            }

            results = await memory.SearchAsync(searchText,
                index: indexName,
                minRelevance: minRelevance,
                limit: top,
                filters: tagFilter);
        }

        List<KernelMemoryDocumentSourceReferenceItem> sourceItems;
        if (documentLibraryName.StartsWith("Additional-"))
        {
            var internalDocumentLibraryName = documentLibraryName.Replace("Additional-", "");
            var documentLibrary = await _documentLibraryInfoService.GetDocumentLibraryByShortNameAsync(internalDocumentLibraryName);

            if (documentLibrary == null)
            {
                _logger.LogError("Document Library {DocumentLibraryName} not found in configuration", internalDocumentLibraryName);
                throw new Exception("Document Library " + internalDocumentLibraryName + " not found in configuration");
            }

            var sourceReferenceItems = new List<DocumentLibrarySourceReferenceItem>();

            foreach (var citation in results.Results)
            {
                var sourceReferenceItem = new DocumentLibrarySourceReferenceItem
                {
                    DocumentLibraryShortName = internalDocumentLibraryName,
                    IndexName = documentLibrary.IndexName
                };
                sourceReferenceItem.SetBasicParameters();
                sourceReferenceItem.AddCitation(citation);

                var firstPartition = citation.Partitions.FirstOrDefault();
                if (firstPartition != null)
                {
                    if (firstPartition.Tags.ContainsKey("OriginalDocumentUrl"))
                    {
                        sourceReferenceItem.SourceReferenceLinkType = SourceReferenceLinkType.SystemNonProxiedUrl;
                        firstPartition.Tags.TryGetValue("OriginalDocumentUrl", out var links);
                        if (links is { Count: > 0 })
                        {
                            sourceReferenceItem.SourceReferenceLink = links.FirstOrDefault();
                        }
                    }
                }

                sourceReferenceItems.Add(sourceReferenceItem);
            }

            sourceItems = [.. sourceReferenceItems];

        }
        else
        {
            //We're dealing with a Document Process and need to create DocumentProcessKnowledgeRepositorySourceReferenceItems 
            //instead of DocumentLibrarySourceReferenceItems. Otherwise, the code is the same as above.

            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentLibraryName);

            if (documentProcess == null)
            {
                _logger.LogError("Document Process {DocumentProcessName} not found in configuration", documentLibraryName);
                throw new Exception("Document Process " + documentLibraryName + " not found in configuration");
            }

            var sourceReferenceItems = new List<DocumentProcessRepositorySourceReferenceItem>();

            foreach (var citation in results.Results)
            {
                var sourceReferenceItem = new DocumentProcessRepositorySourceReferenceItem
                {
                    DocumentProcessShortName = documentLibraryName,
                    IndexName = documentProcess.Repositories[0]
                };
                sourceReferenceItem.SetBasicParameters();
                sourceReferenceItem.AddCitation(citation);

                var firstPartition = citation.Partitions.FirstOrDefault();
                if (firstPartition != null)
                {
                    if (firstPartition.Tags.ContainsKey("OriginalDocumentUrl"))
                    {
                        sourceReferenceItem.SourceReferenceLinkType = SourceReferenceLinkType.SystemNonProxiedUrl;
                        firstPartition.Tags.TryGetValue("OriginalDocumentUrl", out var links);
                        if (links is { Count: > 0 })
                        {
                            sourceReferenceItem.SourceReferenceLink = links.FirstOrDefault();
                        }
                    }
                }

                sourceReferenceItems.Add(sourceReferenceItem);
            }

            sourceItems = [.. sourceReferenceItems];
        }

        return sourceItems;
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