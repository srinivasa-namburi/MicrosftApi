using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public interface IAiCompletionService
{
    Task<List<ContentNode>> GetBodyContentNodes(List<ReportDocument> documents, string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId);
    IAsyncEnumerable<string> GetStreamingBodyContentText(List<ReportDocument> documents, string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId);
}
