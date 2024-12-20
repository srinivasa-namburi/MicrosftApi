using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public interface IBodyTextGenerator
{
   Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId,
        ContentNode? sectionContentNode);
}
