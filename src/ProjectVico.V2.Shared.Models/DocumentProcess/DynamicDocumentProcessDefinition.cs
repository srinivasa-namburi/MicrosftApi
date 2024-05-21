using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Models.DocumentProcess;

public class DynamicDocumentProcessDefinition : EntityBase, IDocumentProcessInfo
{
    public required string ShortName { get; set; }
    public string? Description { get; set; }
    public List<string> Repositories { get; set; } = [];
    public DocumentProcessLogicType LogicType { get; set; }
    public required string BlobStorageContainerName { get; set; }
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";
    public bool ClassifyDocuments { get; set; }
    public string? ClassificationModelName { get; set; }

    public Guid? DocumentOutlineId { get; set; }
    public DocumentOutline? DocumentOutline { get; set; }

    public List<PromptImplementation> Prompts { get; set; } = [];
}