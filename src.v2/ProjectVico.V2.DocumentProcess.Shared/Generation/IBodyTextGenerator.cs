using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public interface IBodyTextGenerator
{
    Task<List<ContentNode>> GenerateBodyText(string contentNodeType, string sectionNumber, string sectionTitle,
        string tableOfContentsString);
}