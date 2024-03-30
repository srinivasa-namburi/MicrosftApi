using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Services.Search;

public class SearchIndexingProcessor : IIndexingProcessor
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly SearchClient _titleSearchClient;
    private readonly SearchClient _sectionSearchClient;
    private readonly SearchClient _customSearchClient;
    private readonly OpenAIClient _openAiClient;

    public SearchIndexingProcessor(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("searchclient-title")]
        SearchClient titleSearchClient,
        [FromKeyedServices("searchclient-section")]
        SearchClient sectionSearchClient,
        [FromKeyedServices("searchclient-customdata")]
        SearchClient customSearchClient,
        [FromKeyedServices("openai-planner")]
        OpenAIClient openAiClient)
    {
        _titleSearchClient = titleSearchClient;
        _sectionSearchClient = sectionSearchClient;
        _customSearchClient = customSearchClient;
        _openAiClient = openAiClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    public bool DeleteAllIndexedDocuments(string indexName)
    {
        Uri serviceEndpoint = new Uri(_serviceConfigurationOptions.CognitiveSearch.Endpoint);
        var searchIndexClient = new SearchIndexClient(serviceEndpoint, new AzureKeyCredential(_serviceConfigurationOptions.CognitiveSearch.Key));

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

    public bool CreateIndex(string indexName)
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
                Configurations = { new SemanticConfiguration(semanticSearchConfigName, new()
                {
                    TitleField = new SemanticField("Title"),
                    ContentFields =
                    {
                        new SemanticField("Content")
                    }
                })}
            },
            Fields =
            {
                new SearchField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchField("Title", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true},
                new SearchField("Type", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("ParentId", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true},
                new SearchField("ParentTitle", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true},
                new SearchField("OriginalFileName", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true},
                new SearchField("OriginalFileHash", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true },
                new SearchField("Content", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
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

        Uri serviceEndpoint = new Uri(_serviceConfigurationOptions.CognitiveSearch.Endpoint);
        var searchIndexClient = new SearchIndexClient(serviceEndpoint, new AzureKeyCredential(_serviceConfigurationOptions.CognitiveSearch.Key));

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

    /// <summary>
    /// This variation of IndexJson allows you to specify the SearchClient to use - which also selects the index to use as that is tied to the SearchClient.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="searchClientWithIndex"></param>
    /// <param name="generateEmbeddings"></param>
    /// <returns></returns>
    public async Task<bool> IndexJson(string json, SearchClient searchClientWithIndex, bool generateEmbeddings = false)
    {
        ReportDocument reportDocument = JsonConvert.DeserializeObject<ReportDocument>(json);

        try
        {

            // If there's a document with the same Title + FileHash, delete it first
            if (reportDocument.OriginalFileHash != null)
            {
                var searchResults = await searchClientWithIndex.SearchAsync<ReportDocument>($"OriginalFileHash:{reportDocument.OriginalFileHash}");
                if (searchResults.HasValue && searchResults.Value.GetResults().Any())
                {
                    var documentsToDelete = searchResults.Value.GetResults()
                        .Where(x => x.Document.Title == reportDocument.Title);

                    IEnumerable<SearchResult<ReportDocument>> toDelete = documentsToDelete.ToList();
                    if (toDelete.Any())
                    {
                        var deleteResult = await searchClientWithIndex.DeleteDocumentsAsync(toDelete.Select(x => x.Document));
                        Console.WriteLine($"Found earlier documents that we deleted for this file : {toDelete.Count()}");
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

    /// <summary>
    /// This variaton of IndexJson will automatically determine whether the root node is of type Title or Heading and use the correct index accordingly.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="generateEmbeddings"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> IndexJson(string json, bool generateEmbeddings = false)
    {
        var reportDocument = JsonConvert.DeserializeObject<ReportDocument>(json);

        if (reportDocument.Type == "Title")
        {
            // We don't generate embeddings for Titles - it's too long to make much sense.
            return await IndexJson(json, _titleSearchClient);
        }

        if (reportDocument.Type == "Heading")
        {
            return await IndexJson(json, _sectionSearchClient, generateEmbeddings);
        }

        if (reportDocument.Type == "Custom")
        {
            return await IndexJson(json, _customSearchClient, generateEmbeddings);
        }

        throw new ArgumentException("Root content node must be of type Title or Heading.");
    }

    public async Task<List<ReportDocument>> SearchWithTitleSearch(string searchText, int top = 12, int k = 7)
    {
        var searchResults = await GetReportDocumentsFromSpecifiedSearchClient(searchText, top, k, _titleSearchClient);
        return searchResults;
    }

    public async Task<List<ReportDocument>> SearchWithCustomSearch(string searchText, int top = 12, int k = 7)
    {
        var searchResults = await GetReportDocumentsFromSpecifiedSearchClient(searchText, top, k, _customSearchClient);
        return searchResults;
    }

    public async Task<List<ReportDocument>> SearchWithHybridSearch(string searchText, int top = 12, int k = 7)
    {
        var searchResults = await GetReportDocumentsFromSpecifiedSearchClient(searchText, top, k, _sectionSearchClient);
        return searchResults;
    }

    private async Task<List<ReportDocument>> GetReportDocumentsFromSpecifiedSearchClient(string searchText, int top, int k, SearchClient searchClient)

    {
        var searchResults = new List<ReportDocument>();

        // Vector-based search
        var queryEmbeddings = await CreateEmbeddingAsync(searchText);
        var searchOptions = new SearchOptions
        {
            VectorSearch = new()
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
            Size = top,
        };

        // First, search for sections or titles in the selected index (through the use of a specified SearchClient
        var vectorSearchResults = searchClient.Search<ReportDocument>(searchText, searchOptions);

        // Add the full section search results after the Title results
        if (vectorSearchResults.Value != null)
        {
            searchResults.AddRange(vectorSearchResults.Value.GetResults().Select(x => x.Document));
        }

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

        var embeddingResult = await _openAiClient.GetEmbeddingsAsync(
            embeddingsOptions: new EmbeddingsOptions(
                _serviceConfigurationOptions.OpenAi.EmbeddingModelDeploymentName, new List<string> { text })
            {
                User = "user"
            });

        Console.WriteLine("Done with embeddings generation");

        var returnValue = embeddingResult.Value.Data[0].Embedding.ToArray();
        return returnValue;
    }

    private async Task<string> SummarizeContentAsync(string text)
    {
        const int MaxSectionLength = 16384; // Adjust based on token limits (this is characters, not tokens)
        var sections = await SplitTextIntoSectionsAsync(text, MaxSectionLength);
        var numberOfSections = sections.Count();

        var maxTokensPerSection = 32768 / numberOfSections;

        var summarizedSections = new List<string?>();

        foreach (var section in sections)
        {

            var summary = await _openAiClient.GetCompletionsAsync(new CompletionsOptions
            {
                DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
                Prompts = { $"Summarize the following text, be as expansive as you can within the limit of {maxTokensPerSection} words:\n\n" + section },
                MaxTokens = maxTokensPerSection, // Adjust as needed

            });

            summarizedSections.Add(summary.Value.Choices.FirstOrDefault()?.Text.Trim());
        }

        Console.WriteLine($"Generated {summarizedSections.Count} summarized sections of max {maxTokensPerSection} words each for embeddings generation");

        // Combine the summaries of all sections
        return string.Join("\n", summarizedSections);
    }

    private async Task<IEnumerable<string>> SplitTextIntoSectionsAsync(string text, int maxSectionLength)
    {
        // The text is likely to be too long for the model to handle, so we need to split it into sections
        var sections = new List<string>();
        var sectionBuilder = new StringBuilder();

        // Split the text into sentences.
        // After splitting, we want to add back in the specific character that was used to split the text.
        // This is because the model uses the punctuation to determine the end of a sentence.
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim() + text[x.Length]);




        foreach (var sent in sentences)
        {
            // If the current sentence would put the section over the max length, start a new section
            if (sectionBuilder.Length + sent.Length > maxSectionLength)
            {
                sections.Add(sectionBuilder.ToString());
                sectionBuilder.Clear();
            }

            sectionBuilder.Append(sent);

        }

        // Add the last section
        sections.Add(sectionBuilder.ToString());
        return sections;

    }



    public string CreateJsonFromReportDocument(ReportDocument reportDocument)
    {
        string json = JsonConvert.SerializeObject(reportDocument, Formatting.Indented);
        return json;
    }

    public string CreateJsonFromContentNode(ContentNode contentNode, Guid? parentId, string? parentTitle, string fileName, Stream hashStream)
    {
        var reportDocument = CreateReportDocumentFromContentNode(contentNode);

        reportDocument.ParentId = parentId?.ToString();
        reportDocument.ParentTitle = parentTitle;
        reportDocument.OriginalFileName = fileName;
        reportDocument.OriginalFileHash = hashStream.GenerateHashFromStreamAndResetStream();

        string json = CreateJsonFromReportDocument(reportDocument);
        return json;
    }

    /// <summary>
    /// This method create a ReportDocument from a ContentNode.
    /// It adds sets the Title to the Text property of the first ContentNode it is passed in
    /// The ContentNode must be of type Title or Heading.
    /// It then merges the text of all children of the ContentNode into the Content property of the ReportDocument, recursively.
    /// </summary>
    /// <param name="node">Root node for processing - must be of type Title or Heading</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ReportDocument CreateReportDocumentFromContentNode(ContentNode node)
    {
        if (node.Type != ContentNodeType.Title && node.Type != ContentNodeType.Heading)
        {
            throw new ArgumentException("Root content node must be of type Title or Heading.");
        }

        var reportDocument = new ReportDocument
        {
            Id = node.Id.ToString(),
            Title = node.Text,
            Type = node.Type.ToString(),
            Content = CombineTextFromChildren(node)
        };

        return reportDocument;
    }


    private string CombineTextFromChildren(ContentNode node)
    {
        var contentBuilder = new StringBuilder();

        foreach (var child in node.Children)
        {
            ProcessNode(child, contentBuilder);
        }

        return contentBuilder.ToString();
    }

    private void ProcessNode(ContentNode node, StringBuilder contentBuilder)
    {
        // Append the text of the current node before processing its children
        if (contentBuilder.Length > 0)
        {
            contentBuilder.AppendLine().AppendLine(); // Add separator between nodes
        }
        contentBuilder.Append(node.Text);

        // Recursively process each child node
        foreach (var child in node.Children)
        {
            ProcessNode(child, contentBuilder);
        }
    }

    public async Task IndexAndStoreContentNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing)
    {

        // We create or update both the Title and Section indexes first
        CreateIndex(_serviceConfigurationOptions.CognitiveSearch.NuclearTitleIndex);
        CreateIndex(_serviceConfigurationOptions.CognitiveSearch.NuclearSectionIndex);

        var i = 0;

        List<string> titleJsonList = new List<string>();
        List<string> sectionJsonList = new List<string>();

        foreach (var contentNode in contentTree)
        {
            // First, generate json for the Title ContentNodes in the root of the content tree
            var json = CreateJsonFromContentNode(contentNode, null, null, baseFileName, streamForHashing);
            titleJsonList.Add(json);
            i++;

            // Next, generate json starting from the Heading ContentNodes in the Children collections of the Title ContentNodes
            foreach (var child in contentNode.Children.Where(x => x.Type == ContentNodeType.Heading))
            {
                json = CreateJsonFromContentNode(child, contentNode.Id, contentNode.Text, baseFileName, streamForHashing);
                sectionJsonList.Add(json);
                i++;
            }
        }

        // Index the Title json strings with Azure Cognitive Search
        foreach (string titleJson in titleJsonList)
        {
            await IndexJson(titleJson, _titleSearchClient, true);
        }

        // Index the Section json strings with Azure Cognitive Search
        foreach (string sectionJson in sectionJsonList)
        {
            await IndexJson(sectionJson, _sectionSearchClient, true);
        }

        Console.WriteLine($"Indexed and stored {i} files for '{baseFileName}'");


    }

    public async Task IndexAndStoreCustomNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing)
    {

        // We create or update the custom indexes first
        CreateIndex(_serviceConfigurationOptions.CognitiveSearch.CustomIndex);

        var i = 0;

        List<string> customJsonList = new List<string>();

        foreach (var contentNode in contentTree)
        {
            // First, generate json for the custom ContentNodes in the root of the content tree
            // Currently, this generates for the titles of the content tree -
            // we might want to index the smaller chunks instead (sectionheadings and below)
            var json = CreateJsonFromContentNode(contentNode, null, null, baseFileName, streamForHashing);
            customJsonList.Add(json);
            i++;
        }

        // Index the Custom json strings with Azure Cognitive Search
        foreach (string customJson in customJsonList)
        {
            await IndexJson(customJson, _customSearchClient, true);
        }

        Console.WriteLine($"Indexed and stored {i} files for '{baseFileName}'");


    }

    public async Task<IEnumerable<ReportDocument>> GetAllUniqueTitlesAsync(int numberOfUniqueFiles)
    {
        //Get all Titles(Chapters) with unique FileHashes from the Title search index. Limit your response to 5 unique environmental report documents
        var searchResults = await _titleSearchClient.SearchAsync<ReportDocument>("*", new SearchOptions
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
}