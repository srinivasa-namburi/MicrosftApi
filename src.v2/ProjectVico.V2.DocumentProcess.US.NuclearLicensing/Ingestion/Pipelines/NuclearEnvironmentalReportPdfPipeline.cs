// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;
using AutoMapper;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Pipelines;
using ProjectVico.V2.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;
using ProjectVico.V2.Shared.Responses;

namespace ProjectVico.V2.DocumentProcess.US.NuclearLicensing.Ingestion.Pipelines;

public class NuclearEnvironmentalReportPdfPipeline : IPdfPipeline
{
    private readonly IContentTreeProcessor _contentTreeProcessor;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly DocumentAnalysisClient _documentAnalysisClient;
    private readonly IMapper _autoMapper;
    private readonly ILogger<NuclearEnvironmentalReportPdfPipeline> _logger;

    private const string LineSeparator = "------------------------------------------------------------------";

    public NuclearEnvironmentalReportPdfPipeline(
        IContentTreeProcessor contentTreeProcessor,
        AzureFileHelper AzureFileHelper,
        DocumentAnalysisClient documentAnalysisClient,
        IMapper autoMapper,
        ILogger<NuclearEnvironmentalReportPdfPipeline> logger)
    {
        _contentTreeProcessor = contentTreeProcessor;
        _azureFileHelper = AzureFileHelper;
        _documentAnalysisClient = documentAnalysisClient;
        _autoMapper = autoMapper;
        _logger = logger;
    }

    public async Task<IngestionPipelineResponse> RunAsync(IngestedDocument document,
        DocumentProcessOptions documentProcessOptions)
    {

        if (document.ClassificationShortCode != "er-numberedchapters")
        {
            _logger.LogWarning("ProcessIngestedDocumentConsumer: Processing stopped because of unsupported classification");
            document.IngestionState = IngestionState.ClassificationUnsupported;
            return new IngestionPipelineResponse() { IsSuccessful = false, UnsupportedClassification = true };
        }

        var response = new IngestionPipelineResponse();

        var stream = await _azureFileHelper.GetFileAsStreamFromFullBlobUrlAsync(document.OriginalDocumentUrl);

        stream.Position = 0;
        // Do all PDF and Content Tree processing here. This class should replicate the process in the console app Program.cs. (ProjectVico.Backend.DocumentIngestion.ConsoleApp)
        // The only difference is that this class should return a list of ContentNodes instead of writing them to a file, since we want to store them in CosmosDB.

        // Grab the PDF file and convert it to a Stream

        var documentIntelligenceAnalysisResult = await AnalyzePdfWithDocumentIntelligenceAsync(document.FileName, stream);

        var allParagraphs = GetDocumentParagraphsCollectionWithDuplicatesRemoved(documentIntelligenceAnalysisResult);

        // For debugging purposes only, get a list of each of SectionHeading and Title Paragraphs
        var sectionHeadings = allParagraphs.Where(p => p.Role == ParagraphRole.SectionHeading).ToList();
        var titles = allParagraphs.Where(p => p.Role == ParagraphRole.Title).ToList();

        // We need to determine the document's structure. We do this by looking at the Paragraphs collection in the AnalyzeResult.
        // If the document has Titles and SectionHeadings (Role) with numeric ordering, then we can assume it has a hierarchical structure.

        var isHierarchical = allParagraphs.Any(p =>
                       p.Role == ParagraphRole.Title || p.Role == ParagraphRole.SectionHeading && Regex.IsMatch(p.Content, @"^\d+(\.\d+)*$"));

        Console.WriteLine("Document structure is hierarchical: " + isHierarchical.ToString());
        List<ContentNode> contentTree = new();
        if (isHierarchical)
        {
            // We use this to determine the parent-child relationship between ContentNodes.
            // If the document has no Titles and SectionHeadings, then we assume it's a flat structure and we don't need to determine the parent-child relationship.
            // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
            // We assume that the ContentNodes are in the correct order in the content tree.
            contentTree = ProcessPdfWithHierarchicalNumberedChapters(allParagraphs);
            response.ContentNodes = contentTree;

            // We want to identify the Paragraphs that are part of Tables as well.
            // We do this by looking at the Paragraphs collection in the AnalyzeResult.
            // Unfortunately, there is no ParagraphRole of type Table. Each cell in a table is a Paragraph with a ParagraphRole of type Paragraph.
            // We need to identify all these paragraphs - usually they are contiguous in the Paragraphs collection.

            var tablesFromDocument = documentIntelligenceAnalysisResult.Tables;
            var ingestionTables = new List<Table>();

            _autoMapper.Map(tablesFromDocument, ingestionTables);
            response.Tables = ingestionTables;

        }
        else
        {
            // If the document doesn't have numbered sectionheadings, then we assume it's a flat structure
            // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
            // We assume that the ContentNodes are in the correct order in the content tree.

            // We haven't currently implemented this logic and will gracefully exit
            Console.WriteLine("Flat structure detected in this document - we can't currently order chapters correctly with this type of structure. Exiting.");
            response.ContentNodes = contentTree;
            return response;
        }

        // Remove reference chapters
        var referenceChapterCount = await _contentTreeProcessor.RemoveReferenceChaptersThroughOpenAiIdentification(contentTree);
        if (referenceChapterCount > 0)
        {
            Console.WriteLine($"Removed {referenceChapterCount} reference chapters");
        }

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
        // Some Paragraphs in the Analysis Result will have a wrong Type. We need to fix this.
        // We handle various scenarios separately.

        // First Scenario : some Page Header elements are wrongly identified as Title or Paragraph (Role null).
        // These can be identified because they repeat on every, or at least most, pages. We either check for
        // DocumentParagraph items in the Paragraphs collection that are duplicates (through the Content property, must be a minimum of 10 duplicates in the collection)

        // Implement code for the first scenario here

        // Make allParagraphs a copy of the Paragraphs collection in the AnalyzeResult - not a reference
        var allParagraphs = new List<DocumentParagraph>(documentIntelligenceAnalysisResult.Paragraphs);


        // Find DocumentParagraphs that are duplicates in the documentIntelligenceAnalysisResult.Paragraphs collection

        var duplicateParagraphs = allParagraphs.GroupBy(x => x.Content)
            .Where(g => g.Count() > 10)
            .Select(y => y.Key)
            .ToList();

        foreach (var duplicateParagraph in duplicateParagraphs)
        {
            // We want to remove all these duplicates from the allParagraphs collection
            allParagraphs.RemoveAll(x => x.Content == duplicateParagraph);
        }

        return allParagraphs;
    }

    private List<ContentNode> ProcessPdfWithHierarchicalNumberedChapters(ICollection<DocumentParagraph> paragraphList)
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

            var originalBoundingRegions = paragraph.BoundingRegions;
            var boundingRegions = new List<V2.Shared.Models.BoundingRegion>();
            _autoMapper.Map(originalBoundingRegions, boundingRegions);
            contentNode.BoundingRegions = boundingRegions;

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
                        contentNode.ParentId = parentContentNode.Id;
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
                ContentNode? parentNode = _contentTreeProcessor.FindLastTitleOrHeading(contentTree);

                if (parentNode != null)
                {
                    contentNode.Type = ContentNodeType.BodyText;
                    parentNode.Children.Add(contentNode);
                    contentNode.ParentId = parentNode.Id;
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
            contentNodeCount += _contentTreeProcessor.CountContentNodes(contentNode);
        }

        Console.WriteLine(
            $"Out of {paragraphList.Count} in the Document Intelligence preparation, we've saved {contentNodeCount} Content Nodes for this document.");

        return contentTree;
    }
}
