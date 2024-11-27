using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.DocumentLibrary;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

public class DynamicDocumentProcessDefinition : EntityBase, IDocumentProcessInfo
{
    public required string ShortName { get; set; }
    public string? Description { get; set; }
    public List<string> Repositories { get; set; } = new();

    public DocumentProcessLogicType LogicType { get; set; }
    public DocumentProcessStatus Status { get; set; } = DocumentProcessStatus.Created;
    public required string BlobStorageContainerName { get; set; }
    public required string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";
    public bool ClassifyDocuments { get; set; }
    public string? ClassificationModelName { get; set; }

    public Guid? DocumentOutlineId { get; set; }
    public DocumentOutline? DocumentOutline { get; set; }

    public List<PromptImplementation> Prompts { get; set; } = [];

    public List<DynamicPluginDocumentProcess>? Plugins { get; set; } = [];

    public List<DocumentLibraryDocumentProcessAssociation>? AdditionalDocumentLibraries { get; set; } = [];
}
