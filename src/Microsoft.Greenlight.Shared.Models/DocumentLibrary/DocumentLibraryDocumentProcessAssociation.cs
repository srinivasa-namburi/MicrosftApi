using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.DocumentLibrary;

public class DocumentLibraryDocumentProcessAssociation : EntityBase
{
    public required Guid DocumentLibraryId { get; set; }
    [JsonIgnore]
    public DocumentLibrary? DocumentLibrary { get; set; }
    public required Guid DynamicDocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DynamicDocumentProcessDefinition { get; set; }
}