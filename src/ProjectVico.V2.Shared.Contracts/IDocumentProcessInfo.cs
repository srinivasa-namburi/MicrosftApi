using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Contracts;

public interface IDocumentProcessInfo
{
    Guid Id { get; set; }
    string ShortName { get; set; }
    string? Description { get; set; }
    List<string> Repositories { get; set; }
    DocumentProcessLogicType LogicType { get; set; }
    string BlobStorageContainerName { get; set; }
    string BlobStorageAutoImportFolderName { get; set; }
    bool ClassifyDocuments { get; set; }
    string? ClassificationModelName { get; set; }
}