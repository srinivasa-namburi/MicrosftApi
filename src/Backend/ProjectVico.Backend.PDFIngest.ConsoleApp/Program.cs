// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared;
using ProjectVico.Backend.DocumentIngestion.Shared.CognitiveSearch.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

public class Program
{
    private static string? s_documentIntelligenceEndpoint = "";
    private static string? s_documentIntelligenceKey = "";
    private static string s_titleIndexName = "dummy-index-01-titles";
    private static string s_sectionIndexName = "dummy-index-01-sections";

    private static IContentTreeProcessor s_contentTreeProcessor;
    private static IIndexingProcessor s_indexingProcessor;
    private static INrcFileProcessor s_nrcFileProcessor;
    private static AiOptions s_aiOptions = new AiOptions();
    private static NrcProcessingOptions s_nrcProcessingOptions = new NrcProcessingOptions();
    private static OpenAIClient s_openAiClient;
    private static ContentTreeJsonTransformer s_contentTreeJsonTransformer;
    private static SearchClient s_titleDocssearchClient;
    private static SearchClient s_sectionDocssearchClient;

    private const string LineSeparator = "--------------------------------------------------------------------";

    static Program()
    {

    }

    public static async Task Main(string[] args)
    {

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddJsonFile(
            path: "appsettings.json",
            optional: true,
            reloadOnChange: true);

        configBuilder.AddJsonFile(
            path: $"appsettings.local.json",
            optional: true,
            reloadOnChange: true);

        configBuilder.AddEnvironmentVariables();

        var configuration = configBuilder.Build();

        // Cache NRC PDF Documents to Azure Blob Storage if the NrcFileProcessing.RunNrcFileProcessor setting is set to true
        // This functionality is enabled/disabled and configured in the NrcFileProcessor section of the appsettings.json file
        configuration.GetSection("NrcFileProcessor").Bind(s_nrcProcessingOptions);
        if (s_nrcProcessingOptions != null)
        {
            if (s_nrcProcessingOptions.NrcFileProcessing.RunNrcFileProcessor == true)
            {
                try
                {
                    s_nrcFileProcessor = new NrcFileProcessor(new OptionsWrapper<NrcProcessingOptions>(s_nrcProcessingOptions));
                    string csvFileName = s_nrcProcessingOptions.NrcFileProcessing.CsvFileName;
                    string csvFileContainerName = s_nrcProcessingOptions.NrcFileProcessing.CsvFileContainerName;
                    await s_nrcFileProcessor.ProcessNrcFilesAsync(csvFileName, csvFileContainerName);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception caught while running NrcFileProcessing functionality: {e}");
                }
            }
        }

        // Instantiate the AiOptions class and bind the configuration values to it
        configuration.GetSection("AI").Bind(s_aiOptions);

        s_documentIntelligenceEndpoint = s_aiOptions.DocumentIntelligence.Endpoint;
        s_documentIntelligenceKey = s_aiOptions.DocumentIntelligence.Key;

        // Instantiate the OpenAIClient class and pass in the AiOptions class
        s_openAiClient = new OpenAIClient(new Uri(s_aiOptions.OpenAI.Endpoint), new AzureKeyCredential(s_aiOptions.OpenAI.Key));

        // Instantiate the SearchClient classes
        s_titleDocssearchClient = new SearchClient(new Uri(s_aiOptions.CognitiveSearch.Endpoint), s_titleIndexName, new AzureKeyCredential(s_aiOptions.CognitiveSearch.Key));
        s_sectionDocssearchClient = new SearchClient(new Uri(s_aiOptions.CognitiveSearch.Endpoint), s_sectionIndexName, new AzureKeyCredential(s_aiOptions.CognitiveSearch.Key));

        // Instantiate the ContentTreeProcessor class and pass in the OpenAIClient class
        s_contentTreeProcessor = new ContentTreeProcessor(new OptionsWrapper<AiOptions>(s_aiOptions), s_openAiClient);

        // Instantiate the IndexingProcessor class and pass in the OpenAIClient class
        s_indexingProcessor = new SearchIndexingProcessor(new OptionsWrapper<AiOptions>(s_aiOptions), s_titleDocssearchClient, s_sectionDocssearchClient, s_openAiClient);

        // Instantiate the ContentTreeJsonTransformer class and pass in the OpenAIClient class
        s_contentTreeJsonTransformer = new ContentTreeJsonTransformer(new OptionsWrapper<AiOptions>(s_aiOptions), s_openAiClient);

        if (string.IsNullOrEmpty(s_documentIntelligenceEndpoint) || string.IsNullOrEmpty(s_documentIntelligenceKey))
        {
            Console.WriteLine(
                "Please set the Document Intelligence endpoint and key in the appsettings.{local}.json file");
            return;
        }

        var pdfDirectory = "./data/";
        var logDirectory = "./logs/";
        var tmpDirectory = "./tmp/";

        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        if (!Directory.Exists(tmpDirectory))
        {
            Directory.CreateDirectory(tmpDirectory);
        }

        if (!Directory.Exists(pdfDirectory))
        {
            Console.WriteLine("pdf directory created. Please put PDF files to be processed in that directory");
            Directory.CreateDirectory(pdfDirectory);
            return;
        }

        var pdfFiles = Directory.GetFiles(pdfDirectory, "*.pdf", SearchOption.AllDirectories);

        if (pdfFiles.Length == 0)
        {
            Console.WriteLine("No PDF files found in the pdf directory");
            return;
        }
        else
        {
            Console.WriteLine($"Found {pdfFiles.Length} PDF files in the pdf directory");
        }

        foreach (var pdfFile in pdfFiles)
        {

            var baseFileName = Path.GetFileNameWithoutExtension(pdfFile);
            //var pdfDocument = PdfDocument.Open(pdfFile);
            List<(string Header, string Text)> headersAndText;

            // Returns a hierarchical list of headers and text
            var contentTree = await ProcessPdfWithDocumentIntelligenceAsync(pdfFile);

            // Remove reference chapters
            var referenceChapterCount = await s_contentTreeProcessor.RemoveReferenceChaptersThroughOpenAiIdentification(contentTree);
            Console.WriteLine($"Removed {referenceChapterCount} reference chapters");

            Console.WriteLine($"Writing output JSON files to tmp directory for '{baseFileName}'");

            await using var pdfStream = File.OpenRead(pdfFile);
            await s_indexingProcessor.IndexAndStoreContentNodesAsync(contentTree, baseFileName, pdfStream);

        }

        var queryInput = "";
        do
        {
            Console.WriteLine("Enter a search query or press enter to exit");
            queryInput = Console.ReadLine();

            if (queryInput == "")
            {
                break;
            }

            var searchResults = await s_indexingProcessor.SearchWithHybridSearch(queryInput, 12, 7);

            Console.WriteLine($"Found {searchResults.Count} results for '{queryInput}'");
            Thread.Sleep(2000);
            Console.WriteLine($"Generating suggested section output with OpenAI based on the top 6 results. Please wait.");

            var suggestedOutput = await GenerateSectionOutputWithOpenAIAsync(searchResults);

            foreach (string s in suggestedOutput)
            {
                Console.WriteLine(s);
            }


        } while (queryInput != "");
    }

    private static async Task<List<string>> GenerateSectionOutputWithOpenAIAsync(List<ReportDocument> sections)
    {
        // Generate example  // Build the examples for the prompt
        var sectionExample = new StringBuilder();

        // Get the 3 first sections
        var firstSections = sections.Take(6).ToList();

        foreach (var section in firstSections)
        {
            sectionExample.AppendLine($"[EXAMPLE: {section.Title}]");
            sectionExample.AppendLine(section.Content);
            sectionExample.AppendLine();
        }

        // Generate section output prompt
        var exampleString = sectionExample.ToString();
        string sectionPrompt =
            $"Below, there are several sections from previous applications. Using this information, write a similar section. Be as verbose as necessary to include all required information. If you are missing details to write specific portions, please indicate that with [DETAIL: <dataType>] - and put the type of data needed in the dataType parameter.\n\n{exampleString}\n\n";

        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        


        var chatResponses = new List<string>();

        // Generate chat completion for section output
        var sectionCompletion = await s_openAiClient.GetChatCompletionsAsync(
            new ChatCompletionsOptions()
            {
                DeploymentName = s_aiOptions.OpenAI.CompletionModel,
                Messages =
                {
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", sectionPrompt)
                },
                MaxTokens = 4096,
                Temperature = 0.3f,
                

            });

        // Get the response from the API
        var chatResponseMessage = sectionCompletion.Value.Choices[0].Message.Content;
        chatResponses.Add(chatResponseMessage);

        var pluginsRequiredPrompt = $"""
                                    Below, there is a section from a Nuclear environmental report. After summarizing this information to yourself, 
                                    summarize the additional data needed that can be retrieved through plugins.
                                    
                                    [SECTION:]
                                    {chatResponseMessage}

                                    [FORMAT AND REQUIRED DATA FOR SECTION]
                                    Please summarize the additional data needed that can be retrieved through plugins.\n
                                    """;

        // Generate chat completion for plugins required
        var pluginsRequiredCompletion = await s_openAiClient.GetChatCompletionsAsync(
                       new ChatCompletionsOptions()
                       {
                DeploymentName = s_aiOptions.OpenAI.CompletionModel,
                Messages =
                {
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", pluginsRequiredPrompt)
                },
                MaxTokens = 4096,
                Temperature = 0.3f,


            });

        // Get the response from the API
        chatResponseMessage = pluginsRequiredCompletion.Value.Choices[0].Message.Content;
        chatResponses.Add(chatResponseMessage);


        return chatResponses;

    }

    private static async Task<List<ContentNode>> ProcessPdfWithDocumentIntelligenceAsync(
        string pdfFile
       )
    {

        // Grab the PDF file and convert it to a Stream
        var pdfContent = File.ReadAllBytes(pdfFile);

        var pdfStream = new MemoryStream(pdfContent);
        var documentIntelligenceAnalysisResult = await AnalyzePdfWithDocumentIntelligenceAsync(pdfFile, pdfStream);

        var allParagraphs = GetDocumentParagraphsCollectionWithDuplicatesRemoved(documentIntelligenceAnalysisResult);


        // For debugging purposes only, get a list of each of SectionHeading and Title Paragraphs
        var sectionHeadings = allParagraphs.Where(p => p.Role == ParagraphRole.SectionHeading).ToList();
        var titles = allParagraphs.Where(p => p.Role == ParagraphRole.Title).ToList();


        // We need to determine the document's structure. We do this by looking at the Paragraphs collection in the AnalyzeResult.
        // If the document has Titles and SectionHeadings (Role) with numeric ordering, then we can assume it has a hierarchical structure.

        var isHierarchical = allParagraphs.Any(p =>
                       p.Role == ParagraphRole.Title || p.Role == ParagraphRole.SectionHeading &&
                                  Regex.IsMatch(p.Content, @"^\d+(\.\d+)*$"));

        Console.WriteLine("Document structure is hierarchical: " + isHierarchical.ToString());

        List<ContentNode> contentTree = new();

        if (isHierarchical)
        {
            // We use this to determine the parent-child relationship between ContentNodes.
            // If the document has no Titles and SectionHeadings, then we assume it's a flat structure and we don't need to determine the parent-child relationship.
            // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
            // We assume that the ContentNodes are in the correct order in the content tree.
            contentTree = await ProcessPdfWithHierarchicalNumberedChaptersAsync(allParagraphs);

            // We want to identify the Paragraphs that are part of Tables as well.
            // We do this by looking at the Paragraphs collection in the AnalyzeResult.
            // Unfortunately, there is no ParagraphRole of type Table. Each cell in a table is a Paragraph with a ParagraphRole of type Paragraph.
            // We need to identify all these paragraphs - usually they are contiguous in the Paragraphs collection.

            var tablesFromDocument = documentIntelligenceAnalysisResult.Tables;
            var tableParagraphs = new List<DocumentParagraph>();


        }
        else
        {
            // If the document doesn't have numbered sectionheadings, then we assume it's a flat structure
            // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
            // We assume that the ContentNodes are in the correct order in the content tree.

            // We haven't currently implemented this logic and will gracefully exit
            Console.WriteLine("Flat structure detected in this document - we can't currently order chapters correctly with this type of structure. Exiting.");
            // Stop program execution
            Environment.Exit(0);


            //contentTree = await ProcessPdfWithFlatStructureAsync(documentIntelligenceAnalysisResult);
        }

        return contentTree;
    }

    private static List<DocumentParagraph> GetDocumentParagraphsCollectionWithDuplicatesRemoved(
        AnalyzeResult documentIntelligenceAnalysisResult)
    {
        // Some Paragraphs in the Analysis Result will have a wrong Type. We need to fix this.
        // We handle various scenarios separately.

        // First Scenario : some Page Header elements are wrongly identified as Title or Paragraph (Role null).
        // These can be identified because they repeat on every, or at least most, pages. We either check for
        // DocumentParagraph items in the Paragraphs collection that are duplicates (through the Content property, must be a minimum of 10 duplicates in the collection)

        // Implement code for the first scenario here

        // Make allParagraphs a copy of the Paragraphs collection in the AnalyzeResult - not a reference
        var allParagraphs = new List<DocumentParagraph>(documentIntelligenceAnalysisResult.Paragraphs);
        var paragraphsToRemove = new List<DocumentParagraph>();

        // Find DocumentParagraphs that are duplicates in the documentIntelligenceAnalysisResult.Paragraphs collection

        var duplicateParagraphs = allParagraphs.GroupBy(x => x.Content)
            .Where(g => g.Count() > 10)
            .Select(y => y.Key)
            .ToList();

        foreach (var duplicateParagraph in duplicateParagraphs)
        {
            var duplicateParagraphsInDocument = allParagraphs.Where(x =>
                x.Content == duplicateParagraph).ToList();

            // We want to remove all these duplicates from the allParagraphs collection

            foreach (var duplicateParagraphInDocument in duplicateParagraphsInDocument)
            {
                paragraphsToRemove.Add(duplicateParagraphInDocument);
            }

            allParagraphs.RemoveAll(x => x.Content == duplicateParagraph);
        }

        return allParagraphs;
    }

    private static async Task<AnalyzeResult> AnalyzePdfWithDocumentIntelligenceAsync(string pdfFile, MemoryStream pdfStream)
    {
        var documentIntelligenceClient = new DocumentAnalysisClient(
            new Uri(s_documentIntelligenceEndpoint),
            new AzureKeyCredential(s_documentIntelligenceKey));

        Console.WriteLine($"Beginning Document Intelligence processing of pdf file '{pdfFile}'");

        Operation<AnalyzeResult> operation = await documentIntelligenceClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout", pdfStream);
        Console.WriteLine("Finished Document Intelligence Processing");
        Console.WriteLine(LineSeparator);
        AnalyzeResult result = operation.Value;
        return result;
    }

    private static async Task<List<ContentNode>> ProcessPdfWithHierarchicalNumberedChaptersAsync(IList<DocumentParagraph> paragraphList)
    {
        var contentTree = new List<ContentNode>();

        Console.WriteLine("Beginning Content Tree construction");
        // This is used to set the previous node in the content tree. We use this to determine if the current node is a duplicate Title.
        var previousContentNode = new ContentNode
        {
            Text = "",
            Children = new List<ContentNode>(),
        };

        // Loop through each paragraph in the documentIntelligenceAnalysisResult.
        foreach (var paragraph in paragraphList)
        {
            var isRootNode = false;
            var contentNode = new ContentNode
            {
                Text = paragraph.Content,
                Children = new List<ContentNode>(),
                Type = ContentNodeType.BodyText // We assume it's a Paragraph unless we find otherwise
            };

            // We want to remove "Chapter", "Section", "Appendix" from the Content,
            // if the paragraph is a Title or SectionHeading so we don't have to handle those in the SectionHeading logic.
            if (paragraph.Role == ParagraphRole.Title || paragraph.Role == ParagraphRole.SectionHeading)
            {
                contentNode.RemoveReservedWordsFromHeading();
            }

            // Each Paragraph has a Role. We're only interested in Title, SectionHeading and Paragraph.
            if (paragraph.Role == ParagraphRole.Title)
            {
                // If the previous paragraph was also a title, remove it from the content tree. We don't want titles after titles.
                if (previousContentNode.Type == ContentNodeType.Title &&
                    !string.IsNullOrEmpty(previousContentNode.Text))
                {
                    Console.WriteLine(
                        $"Removing previous content node due to titles after titles: {previousContentNode.Text}");
                    contentTree.Remove(previousContentNode);
                }

                var sectionNumber = contentNode.Text.Split(' ')[0];
                // We're only interested in Titles that start with a number. If the Title doesn't start with a number, then we discard it.
                if (!Regex.IsMatch(sectionNumber, @"^\d+(\.\d+)*$"))
                {
                    Console.WriteLine($"Discarding Title with no section number: {contentNode.Text}");
                    continue;
                }

                Console.WriteLine($"Adding current paragraph as Title: {paragraph.Content}");
                // If the paragraph is a Title, then we create a new ContentNode and add it to the content tree.
                isRootNode = true;
                contentNode.Type = ContentNodeType.Title;
            }
            else if (paragraph.Role == ParagraphRole.SectionHeading)
            {
                // We need to determined if this SectionHeading is actually a title
                // We do this by checking if the SectionHeading starts with a single number without a period following it.
                // If it has multiple numbers separated by periods, then we assume it's a SectionHeading. The number prior
                // to the first period is the section number - and we use that to determine the parent ContentNode.
                // Periods elsewhere in the SectionHeading are ignored.

                var sectionNumber = contentNode.Text.Split(' ')[0];

                if (!Regex.IsMatch(sectionNumber, @"^\d+(\.\d+)*$"))
                {
                    Console.WriteLine($"Discarding SectionHeading with no section number: {contentNode.Text}");
                    continue;
                }

                // If the SectionHeading has a period following the number, then we assume it's a SectionHeading.
                if (sectionNumber.Contains("."))
                {
                    // If the SectionHeading has multiple numbers separated by periods, then we assume it's a SectionHeading.

                    contentNode.Type = ContentNodeType.Heading;

                    var sectionNumberParts = sectionNumber.Split('.');
                    var sectionNumberToMatch = sectionNumberParts.Length > 1 ? sectionNumberParts[0] : sectionNumber;

                    // First, find the last Title in the Content Tree
                    var parentContentNode = contentTree.FindLast(x =>
                        x.Type == ContentNodeType.Title && x.Text.StartsWith(sectionNumberToMatch));

                    // If the Heading has three or more number parts, we are search for a Heading and need to look in the Children of the last ContentNode.
                    // We need to keep traversing down the tree's Children collections until we find a Heading with a matching section number.
                    if (sectionNumberParts.Length > 2)
                    {
                        for (int i = 1; i < sectionNumberParts.Length - 1; i++)
                        {
                            var sectionNumberPart = sectionNumberParts[i];
                            var sectionNumberPartToMatch = sectionNumberParts[i - 1];

                            if (parentContentNode != null)
                            {
                                parentContentNode = parentContentNode.Children.FindLast(x =>
                                    x.Type == ContentNodeType.Heading && x.Text.StartsWith(sectionNumberPartToMatch));
                            }
                        }
                    }

                    if (parentContentNode != null)
                    {
                        isRootNode = false;
                        parentContentNode.Children.Add(contentNode);
                        contentNode.Parent = parentContentNode;
                        Console.WriteLine($"Adding current paragraph as Heading: {paragraph.Content} under {parentContentNode.Text}");
                    }

                }
                else
                {
                    // This SectionHeading is in fact a Title since it only has a single number with no periods following it.
                    // We add this as a Title to the root of the content tree and continue the loop.

                    Console.WriteLine($"Adding current paragraph as Title: {paragraph.Content}");
                    contentNode.Type = ContentNodeType.Title;
                    isRootNode = true;
                }
            }
            // If no ParagraphRole is specified, then we assume it's a Paragraph. We're not currently storing tables or images.
            else if (paragraph.Role == null)
            {
                isRootNode = false;

                // Find the last Title in the Content Tree through recursion
                ContentNode? parentNode = s_contentTreeProcessor.FindLastTitleOrHeading(contentTree);

                if (parentNode != null)
                {
                    contentNode.Type = ContentNodeType.BodyText;
                    parentNode.Children.Add(contentNode);
                    contentNode.Parent = parentNode;
                    //Console.WriteLine($"Body text added under: {parentNode.Text}");
                }
                else
                {
                    // If no appropriate parent is found, the body text is considered an orphan and is ignored.
                    //Console.WriteLine($"Discarding orphan text: {paragraph.Content}");
                }
            }
            else
            {
                //// The ParagraphRole is not Title, SectionHeading or Paragraph, so we're not interested in it.
                //// Discard and continue the loop.
                continue;
            }

            if (isRootNode)
            {
                contentTree.Add(contentNode);
            }

            previousContentNode = contentNode;
        }

        var contentNodeCount = 0;
        foreach (var contentNode in contentTree)
        {
            contentNodeCount += s_contentTreeProcessor.CountContentNodes(contentNode);
        }

        Console.WriteLine(
            $"Out of {paragraphList.Count} in the Document Intelligence preparation, we've saved {contentNodeCount} Content Nodes for this document.");

        return contentTree;
    }
}
