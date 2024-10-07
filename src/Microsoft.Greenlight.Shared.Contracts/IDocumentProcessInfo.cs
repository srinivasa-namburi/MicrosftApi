using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts;

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
