using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

public interface IDocumentOutlineService
{
    Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument);
}
