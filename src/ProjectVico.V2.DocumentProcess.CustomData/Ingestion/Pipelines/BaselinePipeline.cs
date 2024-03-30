// Copyright (c) Microsoft. All rights reserved.

using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Responses;

namespace ProjectVico.V2.DocumentProcess.CustomData.Pipelines;


public class BaselinePipeline : IPdfPipeline
{

    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly IContentTreeProcessor _contentTreeProcessor;
    private readonly DocumentAnalysisClient _documentAnalysisClient;
    private readonly AzureFileHelper _azureFileHelper;

    private const string LineSeparator = "------------------------------------------------------------------";

    public BaselinePipeline(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IContentTreeProcessor contentTreeProcessor,
        DocumentAnalysisClient documentAnalysisClient,
        AzureFileHelper AzureFileHelper)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _contentTreeProcessor = contentTreeProcessor;
        _documentAnalysisClient = documentAnalysisClient;
        _azureFileHelper = AzureFileHelper;
    }

    public async Task<IngestionPipelineResponse> RunAsync(IngestedDocument document,
        DocumentProcessOptions documentProcessOptions)
    {

        var response = new IngestionPipelineResponse();
        var stream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(document.OriginalDocumentUrl);
        stream!.Position = 0;

        // Do all PDF and Content Tree processing here. This class should replicate the process in the console app Program.cs. (ProjectVico.Backend.DocumentIngestion.ConsoleApp)
        // The only difference is that this class should return a list of ContentNodes instead of writing them to a file, since we want to store them in CosmosDB.

        // Grab the PDF file and convert it to a Stream

        var documentIntelligenceAnalysisResult = await AnalyzePdfWithDocumentIntelligenceAsync(document.FileName, stream);
        var allParagraphs = GetDocumentParagraphsCollectionWithDuplicatesRemoved(documentIntelligenceAnalysisResult);

        // For debugging purposes only, get a list of each of SectionHeading and Title Paragraphs
        var sectionHeadings = allParagraphs.Where(p => p.Role == ParagraphRole.SectionHeading).ToList();
        var titles = allParagraphs.Where(p => p.Role == ParagraphRole.Title).ToList();

        // No assumptions about document's structure (assume it's flat).
        // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
        // We assume that the ContentNodes are in the correct order in the content tree.

        Console.WriteLine("Basic document - process as flat structure.");
        var contentTree = ProcessPdfWithFlatStructure(allParagraphs);

        response.ContentNodes = contentTree;
        return response;
    }

    private async Task<AnalyzeResult> AnalyzePdfWithDocumentIntelligenceAsync(string pdfFile, Stream pdfStream)
    {
        pdfStream.Position = 0;

        Console.WriteLine($"Beginning Document Intelligence processing of pdf file '{pdfFile}'");

        Operation<AnalyzeResult> operation = await _documentAnalysisClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout", pdfStream);
        Console.WriteLine("Finished Document Intelligence Processing");
        Console.WriteLine(LineSeparator);
        AnalyzeResult result = operation.Value;
        return result;
    }


    private List<DocumentParagraph> GetDocumentParagraphsCollectionWithDuplicatesRemoved(
        AnalyzeResult documentIntelligenceAnalysisResult)
    {
        // Make allParagraphs a copy of the Paragraphs collection in the AnalyzeResult - not a reference
        var allParagraphs = new List<DocumentParagraph>(documentIntelligenceAnalysisResult.Paragraphs);

        // Some Paragraphs in the Analysis Result will have a wrong Type. We need to fix this.
        // We handle various scenarios separately.

        // First Scenario : some Page Header elements are wrongly identified as Title or Paragraph (Role null).
        // These can be identified because they repeat on every, or at least most, pages. We either check for
        // DocumentParagraph items in the Paragraphs collection that are duplicates (through the Content property, must be a minimum of 10 duplicates in the collection)

        var duplicateParagraphs = allParagraphs.GroupBy(x => x.Content)
            .Where(g => g.Count() > 10)
            .Select(y => y.Key)
            .ToList();
        
        // Find DocumentParagraphs that are duplicates in the documentIntelligenceAnalysisResult.Paragraphs collection
        foreach (var duplicateParagraph in duplicateParagraphs)
        {
            // We want to remove all these duplicates from the allParagraphs collection
            allParagraphs.RemoveAll(x => x.Content == duplicateParagraph);
        }

        return allParagraphs;
    }


    private List<ContentNode> ProcessPdfWithFlatStructure(IList<DocumentParagraph> paragraphList)
    {
        var contentTree = new List<ContentNode>();

        Console.WriteLine("Beginning Content Tree construction");
        // This is used to set the previous node in the content tree. We use this to determine if the current node is a duplicate Title.
        var previousContentNode = new ContentNode
        {
            Text = "",
            Children = new List<ContentNode>()
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

                Console.WriteLine($"Adding current paragraph as Title: {paragraph.Content}");
                // If the paragraph is a Title or section heading, then we create a new ContentNode and add it to the content tree.
                isRootNode = true;
                contentNode.Type = ContentNodeType.Title;
            }

            else if (paragraph.Role == ParagraphRole.SectionHeading)
            {
                var parentContentNode = contentTree.FindLast(x => x.Type == ContentNodeType.Title);

                if (parentContentNode != null)
                {
                    Console.WriteLine($"Adding current paragraph as Heading: {paragraph.Content} under {parentContentNode.Text}");
                    isRootNode = false;
                    parentContentNode.Children.Add(contentNode);
                    contentNode.Parent = parentContentNode;
                    contentNode.Type = ContentNodeType.Heading;
                }

                else
                {
                    // If no appropriate parent is found, the section header is considered an orphan and is ignored.

                }
            }

            // If no ParagraphRole is specified, then we assume it's a Paragraph. We're not currently storing tables or images.
            else if (paragraph.Role == null)
            {
                isRootNode = false;

                // Find the last Title in the Content Tree through recursion
                ContentNode? parentNode = this._contentTreeProcessor.FindLastTitleOrHeading(contentTree);

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
            contentNodeCount += this._contentTreeProcessor.CountContentNodes(contentNode);
        }

        Console.WriteLine(
            $"Out of {paragraphList.Count} in the Document Intelligence preparation, we've saved {contentNodeCount} Content Nodes for this document.");

        return contentTree;
    }
}
