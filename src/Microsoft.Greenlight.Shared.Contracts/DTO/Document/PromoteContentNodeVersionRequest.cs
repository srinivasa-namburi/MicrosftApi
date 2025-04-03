namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Request to promote a content node version to be the current version.
/// </summary>
public class PromoteContentNodeVersionRequest
{
    /// <summary>
    /// ID of the version to promote.
    /// </summary>
    public required Guid VersionId { get; set; }
    /// <summary>
    /// Comment stored with archiving of the current version.
    /// </summary>
    public string? Comment { get; set; }
}