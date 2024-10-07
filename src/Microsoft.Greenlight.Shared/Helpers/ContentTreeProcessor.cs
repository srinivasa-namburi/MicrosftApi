// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models;


namespace Microsoft.Greenlight.Shared.Helpers;

// Class for Content Tree Processing
public class ContentTreeProcessor : IContentTreeProcessor
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly AzureOpenAIClient _openAiClient;
    

    public ContentTreeProcessor(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions, 
        [FromKeyedServices("openai-planner")] AzureOpenAIClient openAiClient)
    {
        _openAiClient = openAiClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }


    public void FindSectionHeadings(ContentNode contentNode, List<ContentNode> sectionHeadings)
    {
        //Recursively find all Section Headings under this ContentNode
        foreach (var child in contentNode.Children)
        {
            if (child.Type == ContentNodeType.Heading)
            {
                sectionHeadings.Add(child);
            }

            this.FindSectionHeadings(child, sectionHeadings);
        }
    }
    
    public int CountContentNodes(ContentNode contentNode)
    {
        // Count all content nodes in the content tree below this node
        // Traverse the Children property of the ContentNode and recursively call this method
        var count = 1;
        foreach (var child in contentNode.Children)
        {
            count += this.CountContentNodes(child);
        }

        return count;
    }

    public ContentNode? FindLastTitleOrHeading(List<ContentNode> contentTree)
    {
        for (int i = contentTree.Count - 1; i >= 0; i--)
        {
            var node = contentTree[i];
            if (node.Type == ContentNodeType.Title || node.Type == ContentNodeType.Heading)
            {
                // If node has children, recursively find the last Title or Heading node
                var lastChild = this.FindLastTitleOrHeading(node.Children);
                return lastChild ?? node;
            }
        }
        return null;
    }

    private IEnumerable<ContentNode> GetFlattenedContentNodes(IEnumerable<ContentNode> contentNodeChildren)
    {
        foreach (var contentNode in contentNodeChildren)
        {
            yield return contentNode;
            foreach (var child in this.GetFlattenedContentNodes(contentNode.Children))
            {
                yield return child;
            }
        }
    }

    private async Task<List<ContentNode>> GetListOfContentNodesToRemoveFromReferenceChapterListAsync(List<ContentNode> contentTree, List<string> chapterIdentifiers)
    {
        var identifiedNodes = new List<ContentNode>();
        foreach (var chapterIdentifier in chapterIdentifiers)
        {
            this.FindContentNodesMatchingChapterIdentifier(contentTree, chapterIdentifier, identifiedNodes);
        }

        return identifiedNodes;
    }

    private void FindContentNodesMatchingChapterIdentifier(List<ContentNode> contentNodes, string chapterIdentifier, List<ContentNode> identifiedNodes)
    {
        foreach (var contentNode in contentNodes)
        {
            if (string.Equals(contentNode.Text, chapterIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Removing reference chapter: {contentNode.Text}");
                identifiedNodes.Add(contentNode);
            }

            this.FindContentNodesMatchingChapterIdentifier(contentNode.Children, chapterIdentifier, identifiedNodes);
        }
    }

}
