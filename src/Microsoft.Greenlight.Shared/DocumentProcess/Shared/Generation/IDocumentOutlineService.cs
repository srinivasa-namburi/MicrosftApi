using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

public interface IDocumentOutlineService
{
    Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument);
}
