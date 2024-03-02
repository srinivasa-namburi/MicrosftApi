// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;
using ProjectVico.Backend.DocumentIngestion.Shared.Models;
using ProjectVico.Backend.DocumentIngestion.Shared.Options;

namespace ProjectVico.Backend.DocumentIngestion.Shared;

public class ContentTreeJsonTransformer : IContentTreeJsonTransformer
{
    public async Task<List<string>> TransformContentTreeByTitleToConcatenatedJson(List<ContentNode> contentTree)
    {
        List<string> jsonList = this.ProcessContentNodes(contentTree);
        return jsonList;
    }

    public List<string> ProcessContentNodes(List<ContentNode> contentNodes)
    {
        List<string> serializedTitleNodes = new List<string>();

        foreach (var node in contentNodes)
        {
            if (node.Type == ContentNodeType.Title || node.Type == ContentNodeType.Heading)
            {
                var newNode = this.CreateNewNode(node);
                string serializedNode = JsonSerializer.Serialize(newNode, new JsonSerializerOptions { WriteIndented = true });
                serializedTitleNodes.Add(serializedNode);
            }
        }

        return serializedTitleNodes;
    }

    private ContentNode CreateNewNode(ContentNode node)
    {
        var newNode = new ContentNode
        {
            Id = node.Id,
            Text = node.Text,
            Type = node.Type,
            Children = new List<ContentNode>()
        };

        // Combine all BodyText nodes at this level into one
        var bodyTexts = node.Children.Where(n => n.Type == ContentNodeType.BodyText);

        // Combine all body texts into one and add a new line between the paragraphs. Use a StringBuilder for performance.

        StringBuilder sb = new StringBuilder();
        foreach (var bodyText in bodyTexts)
        {
            sb.Append(bodyText.Text);
            sb.Append("\n\n");
        }

        newNode.Children.Add(new ContentNode
        {
            Text = sb.ToString(),
            Type = ContentNodeType.BodyText
        });

        // Process other types of nodes
        var otherChildren = node.Children.Where(n => n.Type != ContentNodeType.BodyText)
            .Select(n => this.CreateNewNode(n));
        newNode.Children.AddRange(otherChildren);

        return newNode;
    }

}
