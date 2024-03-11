using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public interface IDocumentOutlineService
{
    Task<List<ContentNode>> GenerateDocumentOutlineForDocument(GeneratedDocument generatedDocument);
}