using System.Text;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models;
using OpenAI;
using OpenAI.Embeddings;

namespace Microsoft.Greenlight.Shared.Services.Search;

public class SearchIndexingProcessor : IIndexingProcessor
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly SearchClientFactory _searchClientFactory;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public SearchIndexingProcessor(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] 
        AzureOpenAIClient openAiClient,
        SearchClientFactory searchClientFactory
        )
    {
        _openAiClient = openAiClient;
        _searchClientFactory = searchClientFactory;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public bool DeleteIndex(string indexName)
    {
        var searchIndexClient = _searchClientFactory.GetSearchIndexClientForIndex(indexName);

        try
        {
            searchIndexClient.DeleteIndex(indexName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete index. Exception: {ex.Message}");
            return false;
        }

        return true;
    }

    public bool CreateOrUpdateIndex(string indexName)
    {
        var vectorSearchProfileName = _serviceConfigurationOptions.CognitiveSearch.VectorSearchProfileName;
        var vectorSearchHnswConfigName = _serviceConfigurationOptions.CognitiveSearch.VectorSearchHnswConfigName;
        var semanticSearchConfigName = _serviceConfigurationOptions.CognitiveSearch.SemanticSearchConfigName;

        var index = new SearchIndex(indexName)
        {
            VectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile(vectorSearchProfileName, vectorSearchHnswConfigName)
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(vectorSearchHnswConfigName)
                }
            },
            SemanticSearch = new SemanticSearch
            {
                Configurations =
                {
                    new SemanticConfiguration(semanticSearchConfigName, new SemanticPrioritizedFields
                    {
                        TitleField = new SemanticField("Title"),
                        ContentFields =
                        {
                            new SemanticField("Content")
                        }
                    })
                }
            },
            Fields =
            {
                new SearchField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchField("Title", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("Type", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("ParentId", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("ParentTitle", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("OriginalFileName", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true },
                new SearchField("OriginalFileHash", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true },
                new SearchField("Content", SearchFieldDataType.String)
                    { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("TitleVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    VectorSearchDimensions = 1536,
                    VectorSearchProfileName = vectorSearchProfileName
                },
                new SearchField("ContentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    VectorSearchDimensions = 1536,
                    VectorSearchProfileName = vectorSearchProfileName
                }
            }
        };

        var searchIndexClient = _searchClientFactory.GetSearchIndexClientForIndex(indexName);

        // Create or update the index
        try
        {
            searchIndexClient.CreateOrUpdateIndex(index);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create index. Exception: {ex.Message}");
            return false;
        }

        return true;
    }

    public async Task<List<ReportDocument>> SearchSpecifiedIndexAsync(string indexName, string searchText, int top = 12,
        int k = 7)
    {
        var searchClient = GetSearchClient(indexName);

        var searchResults = await GetReportDocumentsFromSpecifiedSearchClient(searchText, top, k, searchClient);
        return searchResults;
    }

   public string CreateJsonFromContentNode(ContentNode contentNode, Guid? parentId, string? parentTitle,
        string fileName, string fileHash)
    {
        var reportDocument = CreateReportDocumentFromContentNode(contentNode);

        reportDocument.ParentId = parentId?.ToString();
        reportDocument.ParentTitle = parentTitle;
        reportDocument.OriginalFileName = fileName;
        reportDocument.OriginalFileHash = fileHash;

        var json = CreateJsonFromReportDocument(reportDocument);
        return json;
    }

    /// <summary>
    ///     This variation of IndexJson allows you to specify the SearchClient to use - which also selects the index to use as
    ///     that is tied to the SearchClient.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="searchClientWithIndex"></param>
    /// <param name="generateEmbeddings"></param>
    /// <returns></returns>
    public async Task<bool> IndexJson(string json, SearchClient searchClientWithIndex, bool generateEmbeddings = false)
    {
        var reportDocument = JsonConvert.DeserializeObject<ReportDocument>(json);

        if (reportDocument == null)
        {
            Console.WriteLine("Failed to deserialize json to ReportDocument");
            return false;
        }

        try
        {
            // If there's a document with the same Title + FileHash, delete it first
            if (reportDocument.OriginalFileHash != null)
            {
                var searchResults =
                    await searchClientWithIndex.SearchAsync<ReportDocument>(
                        $"OriginalFileHash:{reportDocument.OriginalFileHash}");
                if (searchResults.HasValue && searchResults.Value.GetResults().Any())
                {
                    var documentsToDelete = searchResults.Value.GetResults()
                        .Where(x => x.Document.Title == reportDocument.Title);

                    IEnumerable<SearchResult<ReportDocument>> toDelete = documentsToDelete.ToList();
                    if (toDelete.Any())
                    {
                        var deleteResult =
                            await searchClientWithIndex.DeleteDocumentsAsync(toDelete.Select(x => x.Document));
                        Console.WriteLine(
                            $"Found earlier documents that we deleted for this file : {toDelete.Count()}");
                    }
                }
            }

            if (generateEmbeddings)
            {
                // Generate embeddings for the Title and Content fields
                reportDocument.TitleVector = await CreateEmbeddingAsync(reportDocument.Title);

                if (!(reportDocument.Content.Length > 32768))
                {
                    reportDocument.ContentVector = await CreateEmbeddingAsync(reportDocument.Content);
                }
                else
                {
                    reportDocument.ContentVector = Array.Empty<float>();
                    Console.WriteLine("Content too long for embedding generation - skipping");
                }
            }

            // Add the document to the index
            IndexDocumentsBatch<ReportDocument> batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.Upload(reportDocument));

            await searchClientWithIndex.IndexDocumentsAsync(batch);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to index document. Exception: {ex.Message}");
            return false;
        }

        return true;
    }

    public SearchClient GetSearchClient(string indexName)
    {
        return _searchClientFactory.GetSearchClientForIndex(indexName);
    }

    public bool IsEmptyIndex(string indexName)
    {
        // If there are no documents in the index, it is empty
        var searchClient = GetSearchClient(indexName);
        var searchResults = searchClient.Search<ReportDocument>("*", new SearchOptions { Size = 1 });

        return searchResults.Value == null || !searchResults.Value.GetResults().Any();
    }

    /// <summary>
    ///     This method create a ReportDocument from a ContentNode.
    ///     It adds sets the Title to the Text property of the first ContentNode it is passed in
    ///     The ContentNode must be of type Title or Heading.
    ///     It then merges the text of all children of the ContentNode into the Content property of the ReportDocument,
    ///     recursively.
    /// </summary>
    /// <param name="node">Root node for processing - must be of type Title or Heading</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private ReportDocument CreateReportDocumentFromContentNode(ContentNode node)
    {
        if (node.Type != ContentNodeType.Title && node.Type != ContentNodeType.Heading)
            throw new ArgumentException("Root content node must be of type Title or Heading.");

        var reportDocument = new ReportDocument
        {
            Id = node.Id.ToString(),
            Title = node.Text,
            Type = node.Type.ToString(),
            Content = CombineTextFromChildren(node)
        };

        return reportDocument;
    }

    private async Task<List<ReportDocument>> GetReportDocumentsFromSpecifiedSearchClient(string searchText, int top,
        int k, SearchClient searchClient)
    {
        var searchResults = new List<ReportDocument>();

        // Vector-based search
        var queryEmbeddings = await CreateEmbeddingAsync(searchText);
        var searchOptions = new SearchOptions
        {
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryEmbeddings.ToArray())
                    {
                        KNearestNeighborsCount = k,
                        Fields = { "TitleVector", "ContentVector" }
                    }
                }
            },
            Size = top
        };

        // First, search for sections or titles in the selected index (through the use of a specified SearchClient
        var vectorSearchResults = searchClient.Search<ReportDocument>(searchText, searchOptions);

        // Add the full section search results after the Title results
        if (vectorSearchResults.Value != null)
            searchResults.AddRange(vectorSearchResults.Value.GetResults().Select(x => x.Document));

        // Deduplicate the results on the combination of FileHash and Title
        searchResults = searchResults.GroupBy(x => new { x.OriginalFileHash, x.Title }).Select(x => x.First()).ToList();
        return searchResults;
    }


    private async Task<float[]> CreateEmbeddingAsync(string text)
    {
        Console.WriteLine("Generating embedding for text");

        // If the text is too long, cut it.
        if (text.Length > 32768)
        {
            text = text.Substring(0, 32768);
            Console.WriteLine("Text too long for embeddings generation, generating on first 8K tokens");
        }

        var openAiEmbeddingClient =
             _openAiClient.GetEmbeddingClient(_serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName);

        var embeddingResult = await openAiEmbeddingClient.GenerateEmbeddingAsync(text,
            new EmbeddingGenerationOptions() { EndUserId = "user" });

        
        Console.WriteLine("Done with embeddings generation");

        var returnValue = embeddingResult.Value.Vector.ToArray();
        return returnValue;
    }

    private string CreateJsonFromReportDocument(ReportDocument reportDocument)
    {
        var json = JsonConvert.SerializeObject(reportDocument, Formatting.Indented);
        return json;
    }

    private string CombineTextFromChildren(ContentNode node)
    {
        var contentBuilder = new StringBuilder();

        foreach (var child in node.Children) ProcessNode(child, contentBuilder);

        return contentBuilder.ToString();
    }

    private void ProcessNode(ContentNode node, StringBuilder contentBuilder)
    {
        // Append the text of the current node before processing its children
        if (contentBuilder.Length > 0) contentBuilder.AppendLine().AppendLine(); // Add separator between nodes
        contentBuilder.Append(node.Text);

        // Recursively process each child node
        foreach (var child in node.Children) ProcessNode(child, contentBuilder);
    }
}
