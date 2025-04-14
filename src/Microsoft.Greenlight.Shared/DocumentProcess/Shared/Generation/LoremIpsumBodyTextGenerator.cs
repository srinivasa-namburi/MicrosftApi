using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using NLipsum.Core;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

public class LoremIpsumBodyTextGenerator : IBodyTextGenerator
{
    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber, string sectionTitle,
        string tableOfContentsString, string documentProcessName = null, Guid? metadataId = null, ContentNode? sectionContentNode = null)
    {
        var minNumberOfParagraphs = 4;
        var maxNumberOfParagraphs = 16;

        var numberOfParagraphs = new Random().Next(minNumberOfParagraphs, maxNumberOfParagraphs);
         
        // Use the NLipsum library to generate the body text
        var bodyText = LipsumGenerator.Generate(numberOfParagraphs);
        
        // Insert an extra blank line between paragraphs
        bodyText = bodyText.Replace("\n", "\n\n");


        var bodyTextContentNode = new ContentNode()
        {
            Text = bodyText,
            GenerationState = ContentNodeGenerationState.Completed,
            Type = ContentNodeType.BodyText

        };

        var bodyTextContentNodes = new List<ContentNode> { bodyTextContentNode };
        return bodyTextContentNodes;
    }
}
