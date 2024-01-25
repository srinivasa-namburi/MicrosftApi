// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Pipelines;

public class BaselinePipeline : IPdfPipeline
{

    private readonly AiOptions _aiOptions;
    private readonly IContentTreeProcessor _contentTreeProcessor;
    private readonly IContentTreeJsonTransformer _contentTreeJsonTransformer;

    private const string LineSeparator = "------------------------------------------------------------------";

    public BaselinePipeline(
        IOptions<AiOptions> aiOptions,
        IContentTreeProcessor contentTreeProcessor,
        IContentTreeJsonTransformer contentTreeJsonTransformer)
    {
        this._aiOptions = aiOptions.Value;
        this._contentTreeProcessor = contentTreeProcessor;
        this._contentTreeJsonTransformer = contentTreeJsonTransformer;
    }

    public async Task<List<ContentNode>> RunAsync(MemoryStream pdfStream, string pdfName)
    {
        pdfStream.Position = 0;
        // Do all PDF and Content Tree processing here. This class should replicate the process in the console app Program.cs. (ProjectVico.Backend.DocumentIngestion.ConsoleApp)
        // The only difference is that this class should return a list of ContentNodes instead of writing them to a file, since we want to store them in CosmosDB.

        // Grab the PDF file and convert it to a Stream

        var documentIntelligenceAnalysisResult = await this.AnalyzePdfWithDocumentIntelligenceAsync(pdfName, pdfStream);

        var allParagraphs = this.GetDocumentParagraphsCollectionWithDuplicatesRemoved(documentIntelligenceAnalysisResult);

        // For debugging purposes only, get a list of each of SectionHeading and Title Paragraphs
        var sectionHeadings = allParagraphs.Where(p => p.Role == ParagraphRole.SectionHeading).ToList();
        var titles = allParagraphs.Where(p => p.Role == ParagraphRole.Title).ToList();

        // No assumptions about document's structure (assume it's flat).
        // We still need to determine the parent-child relationship between ContentNodes, but we do this by looking at the order of the ContentNodes in the content tree.
        // We assume that the ContentNodes are in the correct order in the content tree.

        List<ContentNode> contentTree = new();

        Console.WriteLine("Basic document - process as flat structure.");
        contentTree = this.ProcessPdfWithFlatStructure(allParagraphs);

        return contentTree;
    }

    private async Task<AnalyzeResult> AnalyzePdfWithDocumentIntelligenceAsync(string pdfFile, MemoryStream pdfStream)
    {
        var documentIntelligenceClient = new DocumentAnalysisClient(
            new Uri(this._aiOptions.DocumentIntelligence.Endpoint),
            new AzureKeyCredential(this._aiOptions.DocumentIntelligence.Key));

        pdfStream.Position = 0;

        Console.WriteLine($"Beginning Document Intelligence processing of pdf file '{pdfFile}'");

        Operation<AnalyzeResult> operation = await documentIntelligenceClient.AnalyzeDocumentAsync(
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


    private List<ContentNode> ProcessPdfWithFlatStructure(IList<DocumentParagraph> paragraphList)
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

            // Each Paragraph has a Role. We're only interested in Title, SectionHeading and Paragraph.
            if (paragraph.Role == ParagraphRole.Title || paragraph.Role == ParagraphRole.SectionHeading)
            {

                // If the previous paragraph was also a title, remove it from the content tree. We don't want titles after titles.
                if (previousContentNode.Type == ContentNodeType.Title &&
                    !string.IsNullOrEmpty(previousContentNode.Text))
                {
                    Console.WriteLine(
                        $"Removing previous content node due to titles after titles: {previousContentNode.Text}");
                    contentTree.Remove(previousContentNode);
                }

                Console.WriteLine($"Adding current paragraph as Title or Section Header: {paragraph.Content}");
                // If the paragraph is a Title or section heading, then we create a new ContentNode and add it to the content tree.
                isRootNode = true;
                contentNode.Type = ContentNodeType.Title;
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
