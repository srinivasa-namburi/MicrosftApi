// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.Models;


namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Class for Content Tree Processing
/// </summary>
public class ContentTreeProcessor : IContentTreeProcessor
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly AzureOpenAIClient _openAiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTreeProcessor"/> class.
    /// </summary>
    /// <param name="serviceConfigurationOptions">The service configuration options.</param>
    /// <param name="openAiClient">The Azure OpenAI client.</param>
    public ContentTreeProcessor(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] AzureOpenAIClient openAiClient)
    {
        _openAiClient = openAiClient;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
    }

    /// <summary>
    /// Recursively finds all Section Headings under this ContentNode.
    /// </summary>
    /// <param name="contentNode">The content node to start searching from.</param>
    /// <param name="sectionHeadings">The list to store found section headings.</param>
    public void FindSectionHeadings(ContentNode contentNode, List<ContentNode> sectionHeadings)
    {
        foreach (var child in contentNode.Children)
        {
            if (child.Type == ContentNodeType.Heading)
            {
                sectionHeadings.Add(child);
            }

            this.FindSectionHeadings(child, sectionHeadings);
        }
    }

    /// <summary>
    /// Counts all content nodes in the content tree below this node.
    /// </summary>
    /// <param name="contentNode">The content node to start counting from.</param>
    /// <returns>The total number of content nodes.</returns>
    public int CountContentNodes(ContentNode contentNode)
    {
        var count = 1;
        foreach (var child in contentNode.Children)
        {
            count += this.CountContentNodes(child);
        }

        return count;
    }

    /// <summary>
    /// Finds the last Title or Heading node in the content tree.
    /// </summary>
    /// <param name="contentTree">The content tree to search.</param>
    /// <returns>The last Title or Heading node, or null if none found.</returns>
    public ContentNode? FindLastTitleOrHeading(List<ContentNode> contentTree)
    {
        for (int i = contentTree.Count - 1; i >= 0; i--)
        {
            var node = contentTree[i];
            if (node.Type == ContentNodeType.Title || node.Type == ContentNodeType.Heading)
            {
                var lastChild = this.FindLastTitleOrHeading(node.Children);
                return lastChild ?? node;
            }
        }
        return null;
    }
}
