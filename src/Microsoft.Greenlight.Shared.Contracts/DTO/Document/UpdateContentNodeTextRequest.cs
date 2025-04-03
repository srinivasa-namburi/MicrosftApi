using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents a request to update the text of a content node. It includes the new text, a versioning reason, and an
/// optional comment (triggers a new version).
/// </summary>
public class UpdateContentNodeTextRequest
{
    /// <summary>
    /// New text for the content node.
    /// </summary>
    public required string NewText { get; set; }
    /// <summary>
    /// Versioning reason for the change. Default is ManualEdit.
    /// </summary>
    public ContentNodeVersioningReason VersioningReason { get; set; } = ContentNodeVersioningReason.ManualEdit;
    /// <summary>
    /// Comment stored with the versioning of the current state.
    /// </summary>
    public string? Comment { get; set; }
}