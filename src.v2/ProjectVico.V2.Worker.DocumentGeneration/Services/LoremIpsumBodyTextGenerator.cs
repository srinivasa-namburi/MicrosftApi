using BAMCIS.GeoJSON;
using NLipsum.Core;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Worker.DocumentGeneration.Services;

public class LoremIpsumBodyTextGenerator : IBodyTextGenerator
{
    public async Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber,
        string sectionTitle, string tableOfContentsString = "")
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