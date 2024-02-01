// Copyright (c) Microsoft. All rights reserved.

using Azure.Search.Documents;
using ProjectVico.Backend.DocumentIngestion.Shared.CognitiveSearch.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;

public interface IIndexingProcessor
{
    Task<bool> IndexJson(string json, SearchClient searchClientWithIndex, bool generateEmbeddings=false);
    bool CreateIndex(string indexName);
    string CreateJsonFromReportDocument(ReportDocument reportDocument);

    string CreateJsonFromContentNode(
        ContentNode contentNode,
        Guid? parentId,
        string? parentTitle,
        string? fileName,
        Stream hashStream);

    /// <summary>
    /// This method create a ReportDocument from a ContentNode.
    /// It adds sets the Title to the Text property of the first ContentNode it is passed in
    /// The ContentNode must be of type Title or Heading.
    /// It then merges the text of all children of the ContentNode into the Content property of the ReportDocument, recursively.
    /// </summary>
    /// <param name="node">Root node for processing - must be of type Title or Heading</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    ReportDocument CreateReportDocumentFromContentNode(ContentNode node);

    /// <summary>
    /// This variaton of IndexJson will automatically determine whether the root node is of type Title or Heading and use the correct index accordingly.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="generateEmbeddings"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    Task<bool> IndexJson(string json, bool generateEmbeddings = false);

    Task<List<ReportDocument>> SearchWithHybridSearch(string searchText, int top = 12, int k = 7);
    Task IndexAndStoreContentNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing);
    Task IndexAndStoreCustomNodesAsync(List<ContentNode> contentTree, string baseFileName, Stream streamForHashing);

}
