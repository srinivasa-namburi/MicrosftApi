using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public interface IAiCompletionService
{
    Task<List<ContentNode>> GetBodyContentNodes(List<ReportDocument> documents, string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId);
    IAsyncEnumerable<string> GetStreamingBodyContentText(List<ReportDocument> documents, string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId);
}