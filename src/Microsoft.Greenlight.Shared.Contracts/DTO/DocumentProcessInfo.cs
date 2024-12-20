using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public class DocumentProcessInfo : IDocumentProcessInfo
{
    public Guid Id { get; set; } = Guid.Empty;
    public string ShortName { get; set; }
    public string? Description { get; set; }

    public List<string> Repositories { get; set; } = [];

    public DocumentProcessLogicType LogicType { get; set; } = DocumentProcessLogicType.KernelMemory;

    public string BlobStorageContainerName { get; set; }
    public string BlobStorageAutoImportFolderName { get; set; } = "ingest-auto";

    public bool ClassifyDocuments { get; set; } = false;
    public string? ClassificationModelName { get; set; }

    public ProcessSource Source => Id == Guid.Empty ? ProcessSource.Static : ProcessSource.Dynamic;

    public Guid? DocumentOutlineId { get; set; }
    public string? OutlineText { get; set; }
    public int PrecedingSearchPartitionInclusionCount { get; set; } = 0;
    public int FollowingSearchPartitionInclusionCount { get; set; } = 0;
    public int NumberOfCitationsToGetFromRepository { get; set; } = 50;
    public double MinimumRelevanceForCitations { get; set; } = 0.7;
}
