using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.DocumentLibrary;

/// <summary>
/// Represents the association between a document library and a dynamic document process definition.
/// </summary>
public class DocumentLibraryDocumentProcessAssociation : EntityBase
{
    /// <summary>
    /// Unique identifier of the document library.
    /// </summary>
    public required Guid DocumentLibraryId { get; set; }

    /// <summary>
    /// Document library associated with this association.
    /// </summary>
    [JsonIgnore]
    public DocumentLibrary? DocumentLibrary { get; set; }

    /// <summary>
    /// Unique identifier of the dynamic document process definition.
    /// </summary>
    public required Guid DynamicDocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Dynamic document process definition associated with this association.
    /// </summary>
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DynamicDocumentProcessDefinition { get; set; }
}
