// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Backend.DocumentIngestion.Shared;

// Class for Content Tree Processing
public class ContentTreeProcessor : IContentTreeProcessor
{
    private readonly string _openAiEndpoint;
    private readonly string _openAiKey;
    private readonly string _openAiCompletionModel;
    private readonly AiOptions _aiOptions;

    public ContentTreeProcessor(IOptions<AiOptions> aiOptions, OpenAIClient openAiClient)
    {
        this._aiOptions = aiOptions.Value;
        this._openAiEndpoint = this._aiOptions.OpenAI.Endpoint;
        this._openAiKey = this._aiOptions.OpenAI.Key;
        this._openAiCompletionModel = this._aiOptions.OpenAI.CompletionModel;
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

    public async Task<int> RemoveReferenceChaptersThroughOpenAiIdentification(List<ContentNode> contentTree)
    {
        // Use OpenAI to identify chapters that are typical headings for chapters that contain only references, bibliography, appendices, etc.
        // We use the OpenAI API to identify these chapters. We then remove them from the content tree.
        // We use the OpenAI API because it's easier to use than the Document Intelligence API for this purpose.

        var chapterIdentifierSamples = new List<string>
        {
            "references",
            "bibliography",
            "appendix",
            "appendices",
            "tables"
        };

        var chapterIdentifierSamplesJson = JsonSerializer.Serialize(chapterIdentifierSamples);

        Uri endPointUri = new Uri(this._openAiEndpoint);

        // Use the OpenAI SDK to call the API
        var client = new OpenAIClient(endpoint: endPointUri, new AzureKeyCredential(this._openAiKey));

        // Flatten the content tree into a list of Text - grab all content nodes of type Title or Heading recursively,
        // traversing the content tree and the Children of each ContentNode

        var flattenedContentTree = this.GetFlattenedContentNodes(contentTree);

        // Generate a prompt for the API
        var prompt = new StringBuilder();
        prompt.AppendLine("This is a list of chapters in a book. Please list the full name of each heading you think sounds like")
            .AppendLine("a chapter that contains only these types of things - and synonyms of these types of things :")
            .AppendLine(chapterIdentifierSamplesJson)
            .AppendLine("Here is the list of Chapters:")
            .AppendLine(string.Join("\n", contentTree.Select(x => x.Text)))
            ;

        // Call the API using the CreateChatCompletionAsync method
        var response = await client.GetChatCompletionsAsync(new ChatCompletionsOptions()
        {
            Messages = { new ChatMessage("user", prompt.ToString()) },
            MaxTokens = 100,
            Temperature = 0.5f,
            DeploymentName = this._openAiCompletionModel
        });

        // Get the response from the API
        var chatResponseMessage = response.Value.Choices[0].Message.Content;

        // Parse the response from the API
        var chapterIdentifiers = new List<string>();

        // Each chapter identifier is on its own line - add them to the list
        foreach (var line in chatResponseMessage.Split('\n'))
        {
            var chapterIdentifier = line.Trim();
            if (!string.IsNullOrEmpty(chapterIdentifier))
            {
                chapterIdentifiers.Add(chapterIdentifier);
            }
        }

        Console.WriteLine($"Identified chapters for removal: {string.Join(", ", chapterIdentifiers)}");

        // Remove reference chapters with more caution
        var nodesToRemove = await this.GetListOfContentNodesToRemoveFromReferenceChapterListAsync(contentTree, chapterIdentifiers);

        foreach (var node in nodesToRemove)
        {
            contentTree.Remove(node);
        }

        return nodesToRemove.Count;
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
