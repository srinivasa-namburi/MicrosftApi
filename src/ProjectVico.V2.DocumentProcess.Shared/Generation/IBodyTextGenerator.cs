using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public interface IBodyTextGenerator
{
   Task<List<ContentNode>> GenerateBodyText(string contentNodeTypeString, string sectionNumber,
        string sectionTitle, string tableOfContentsString, string documentProcessName, Guid? metadataId);
}