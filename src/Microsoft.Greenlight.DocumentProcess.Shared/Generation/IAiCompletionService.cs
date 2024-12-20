using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public interface IAiCompletionService
{
    Task<List<ContentNode>> GetBodyContentNodes(List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId,
        ContentNode? sectionContentNode);
    IAsyncEnumerable<string> GetStreamingBodyContentText(
        List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments, 
        string sectionOrTitleNumber,
        string sectionOrTitleText, ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId, 
        ContentNode? sectionContentNode);
}
