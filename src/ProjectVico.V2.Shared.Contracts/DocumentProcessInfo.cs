using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Contracts;

public class DocumentProcessInfo : IDocumentProcessInfo
{
    public required string ShortName { get; set; }
    public string? Description { get; set; }

    public List<string> Repositories { get; set; } = [];

    public DocumentProcessLogicType LogicType { get; set; }

    public required string BlobStorageContainerName { get; set; }
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    public bool ClassifyDocuments { get; set; } = false;
    public string? ClassificationModelName { get; set; }
}